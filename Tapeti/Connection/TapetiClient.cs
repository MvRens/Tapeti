using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Tapeti.Config;
using Tapeti.Default;
using Tapeti.Exceptions;
using Tapeti.Helpers;

namespace Tapeti.Connection
{
    /// <summary>
    /// Implementation of ITapetiClient for the RabbitMQ Client library
    /// </summary>
    internal class TapetiClient : ITapetiClient
    {
        private const int MandatoryReturnTimeout = 300000;
        private const int CloseMessageHandlersTimeout = 30000;

        private readonly TapetiConnectionParams connectionParams;

        private readonly ITapetiConfig config;
        private readonly ILogger logger;


        private readonly TapetiClientConnection connection;
        private readonly TapetiChannel defaultConsumeChannel;
        private readonly TapetiChannel defaultPublishChannel;
        private readonly List<TapetiChannel> dedicatedChannels = new();

        private readonly HttpClient managementClient;
        private readonly MessageHandlerTracker messageHandlerTracker = new();

        // These fields are for use in a single TapetiChannel's queue only!
        private ulong lastDeliveryTag;
        private readonly HashSet<string> deletedQueues = new();

        // These fields must be locked using confirmLock, since the callbacks for BasicAck/BasicReturn can run in a different thread
        private readonly object confirmLock = new();
        private readonly Dictionary<ulong, ConfirmMessageInfo> confirmMessages = new();
        private readonly Dictionary<string, ReturnInfo> returnRoutingKeys = new();


        private class ConfirmMessageInfo
        {
            public string ReturnKey { get; }
            public TaskCompletionSource<int> CompletionSource { get; }


            public ConfirmMessageInfo(string returnKey, TaskCompletionSource<int> completionSource)
            {
                ReturnKey = returnKey;
                CompletionSource = completionSource;
            }
        }


        private class ReturnInfo
        {
            public uint RefCount;
            public int FirstReplyCode;
        }


        public TapetiClient(ITapetiConfig config, TapetiConnectionParams connectionParams, IConnectionEventListener? connectionEventListener)
        {
            this.config = config;
            this.connectionParams = connectionParams;

            logger = config.DependencyResolver.Resolve<ILogger>();

            connection = new TapetiClientConnection(logger, connectionParams) 
            {
                ConnectionEventListener = connectionEventListener
            };

            defaultConsumeChannel = connection.CreateChannel(InitConsumeModel);
            defaultPublishChannel = connection.CreateChannel(InitPublishModel);

            connection.OnQueueReconnect += () => defaultConsumeChannel.QueueRetryable(_ => { });


            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(connectionParams.Username, connectionParams.Password)
            };

            managementClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            managementClient.DefaultRequestHeaders.Add("Connection", "close");
        }


        private void InitConsumeModel(IModel model)
        {
            if (connectionParams.PrefetchCount > 0)
                model.BasicQos(0, connectionParams.PrefetchCount, false);
        }


        private void InitPublishModel(IModel model)
        {
            if (config.GetFeatures().PublisherConfirms)
            {
                lastDeliveryTag = 0;

                Monitor.Enter(confirmLock);
                try
                {
                    foreach (var pair in confirmMessages)
                        pair.Value.CompletionSource.SetCanceled();

                    confirmMessages.Clear();
                }
                finally
                {
                    Monitor.Exit(confirmLock);
                }

                model.ConfirmSelect();
            }

            model.BasicReturn += HandleBasicReturn;
            model.BasicAcks += HandleBasicAck;
            model.BasicNacks += HandleBasicNack;
        }


        /// <inheritdoc />
        public async Task Publish(byte[] body, IMessageProperties properties, string? exchange, string routingKey, bool mandatory)
        {
            if (string.IsNullOrEmpty(routingKey))
                throw new ArgumentNullException(nameof(routingKey));


            await defaultPublishChannel.QueueWithProvider(async channelProvider =>
            {
                Task<int>? publishResultTask = null;
                var messageInfo = new ConfirmMessageInfo(GetReturnKey(exchange ?? string.Empty, routingKey), new TaskCompletionSource<int>());


                channelProvider.WithRetryableChannel(channel =>
                {
                    if (exchange != null)
                        DeclareExchange(channel, exchange);

                    // The delivery tag is lost after a reconnect, register under the new tag
                    if (config.GetFeatures().PublisherConfirms)
                    {
                        lastDeliveryTag++;

                        Monitor.Enter(confirmLock);
                        try
                        {
                            confirmMessages.Add(lastDeliveryTag, messageInfo);
                        }
                        finally
                        {
                            Monitor.Exit(confirmLock);
                        }

                        publishResultTask = messageInfo.CompletionSource.Task;
                    }
                    else
                        mandatory = false;

                    try
                    {
                        var publishProperties = new RabbitMQMessageProperties(channel.CreateBasicProperties(), properties);
                        channel.BasicPublish(exchange ?? string.Empty, routingKey, mandatory, publishProperties.BasicProperties, body);
                    }
                    catch
                    {
                        messageInfo.CompletionSource.SetCanceled();
                        publishResultTask = null;

                        throw;
                    }
                });


                if (publishResultTask == null)
                    return;

                var delayCancellationTokenSource = new CancellationTokenSource();
                var signalledTask = await Task.WhenAny(
                    publishResultTask,
                    Task.Delay(MandatoryReturnTimeout, delayCancellationTokenSource.Token)).ConfigureAwait(false);

                if (signalledTask != publishResultTask)
                    throw new TimeoutException(
                        $"Timeout while waiting for basic.return for message with exchange '{exchange}' and routing key '{routingKey}'");

                delayCancellationTokenSource.Cancel();

                if (publishResultTask.IsCanceled)
                    throw new NackException(
                        $"Mandatory message with with exchange '{exchange}' and routing key '{routingKey}' was nacked");

                var replyCode = publishResultTask.Result;

                switch (replyCode)
                {
                    // There is no RabbitMQ.Client.Framing.Constants value for this "No route" reply code
                    // at the time of writing...
                    case 312:
                        throw new NoRouteException(
                            $"Mandatory message with exchange '{exchange}' and routing key '{routingKey}' does not have a route");

                    case > 0:
                        throw new NoRouteException(
                            $"Mandatory message with exchange '{exchange}' and routing key '{routingKey}' could not be delivered, reply code: {replyCode}");
                }
            }).ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task<ITapetiConsumerTag?> Consume(string queueName, IConsumer consumer, TapetiConsumeOptions options, CancellationToken cancellationToken)
        {
            if (deletedQueues.Contains(queueName))
                return null;

            if (string.IsNullOrEmpty(queueName))
                throw new ArgumentNullException(nameof(queueName));


            long capturedConnectionReference = -1;
            string? consumerTag = null;

            var channel = options.DedicatedChannel
                ? CreateDedicatedConsumeChannel()
                : defaultConsumeChannel;

            await channel.QueueRetryable(model =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                capturedConnectionReference = connection.GetConnectionReference();
                var basicConsumer = new TapetiBasicConsumer(consumer, messageHandlerTracker, capturedConnectionReference, 
                    (connectionReference, deliveryTag, result) => Respond(channel, connectionReference, deliveryTag, result));

                consumerTag = model.BasicConsume(queueName, false, basicConsumer);
            }).ConfigureAwait(false);

            return consumerTag == null 
                ? null 
                : new TapetiConsumerTag(this, channel, consumerTag, capturedConnectionReference);
        }


        private async Task Cancel(TapetiChannel channel, string consumerTag, long connectionReference)
        {
            if (connection.IsClosing || string.IsNullOrEmpty(consumerTag))
                return;

            var capturedConnectionReference = connection.GetConnectionReference();

            // If the connection was re-established in the meantime, don't respond with an
            // invalid deliveryTag. The message will be requeued.
            if (capturedConnectionReference != connectionReference)
                return;

            // No need for a retryable channel here, if the connection is lost
            // so is the consumer.
            await channel.Queue(model =>
            {
                // Check again as a reconnect may have occured in the meantime
                var currentConnectionReference = connection.GetConnectionReference();
                if (currentConnectionReference != connectionReference)
                    return;

                model.BasicCancel(consumerTag);
            }).ConfigureAwait(false);
        }


        private async Task Respond(TapetiChannel channel, long expectedConnectionReference, ulong deliveryTag, ConsumeResult result)
        {
            await channel.Queue(model =>
            {
                // If the connection was re-established in the meantime, don't respond with an
                // invalid deliveryTag. The message will be requeued.
                var currentConnectionReference = connection.GetConnectionReference();
                if (currentConnectionReference != expectedConnectionReference)
                    return;

                // No need for a retryable channel here, if the connection is lost we can't
                // use the deliveryTag anymore.
                switch (result)
                {
                    case ConsumeResult.Success:
                    case ConsumeResult.ExternalRequeue:
                        model.BasicAck(deliveryTag, false);
                        break;

                    case ConsumeResult.Error:
                        model.BasicNack(deliveryTag, false, false);
                        break;

                    case ConsumeResult.Requeue:
                        model.BasicNack(deliveryTag, false, true);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(result), result, null);
                }
            }).ConfigureAwait(false);
        }


        private async Task<bool> GetDurableQueueDeclareRequired(string queueName, IRabbitMQArguments? arguments)
        {
            var existingQueue = await GetQueueInfo(queueName).ConfigureAwait(false);
            if (existingQueue == null) 
                return true;
            
            if (!existingQueue.Durable || existingQueue.AutoDelete || existingQueue.Exclusive)
                throw new InvalidOperationException($"Durable queue {queueName} already exists with incompatible parameters, durable = {existingQueue.Durable} (expected True), autoDelete = {existingQueue.AutoDelete} (expected False), exclusive = {existingQueue.Exclusive} (expected False)");

            var existingArguments = ConvertJsonArguments(existingQueue.Arguments);
            if (existingArguments.NullSafeSameValues(arguments))
                return true;

            (logger as IBindingLogger)?.QueueExistsWarning(queueName, existingArguments, arguments);
            return false;
        }


        private static RabbitMQArguments? ConvertJsonArguments(IReadOnlyDictionary<string, JValue>? arguments)
        {
            if (arguments == null)
                return null;

            var result = new RabbitMQArguments();
            foreach (var pair in arguments)
            {
                // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault - by design
                object value = pair.Value.Type switch
                {
                    JTokenType.Integer => pair.Value.Value<int>(),
                    JTokenType.Float => pair.Value.Value<double>(),
                    JTokenType.String => pair.Value.Value<string>() ?? string.Empty,
                    JTokenType.Boolean => pair.Value.Value<bool>(),
                    _ => throw new ArgumentOutOfRangeException(nameof(arguments))
                };

                result.Add(pair.Key, value);
            }

            return result;
        }



        /// <inheritdoc />
        public async Task DurableQueueDeclare(string queueName, IEnumerable<QueueBinding> bindings, IRabbitMQArguments? arguments, CancellationToken cancellationToken)
        {
            var declareRequired = await GetDurableQueueDeclareRequired(queueName, arguments).ConfigureAwait(false);

            var existingBindings = (await GetQueueBindings(queueName).ConfigureAwait(false)).ToList();
            var currentBindings = bindings.ToList();
            var bindingLogger = logger as IBindingLogger;

            // Metadata operations are performed at startup so they can always run on the defaultConsumeChannel,
            // regardless of whether the consumer of the queue will use a dedicated channel.
            await defaultConsumeChannel.Queue(channel =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (declareRequired)
                {
                    bindingLogger?.QueueDeclare(queueName, true, false);
                    channel.QueueDeclare(queueName, true, false, false, GetDeclareArguments(arguments));
                }

                foreach (var binding in currentBindings.Except(existingBindings))
                {
                    DeclareExchange(channel, binding.Exchange);
                    bindingLogger?.QueueBind(queueName, true, binding.Exchange, binding.RoutingKey);
                    channel.QueueBind(queueName, binding.Exchange, binding.RoutingKey);
                }

                foreach (var deletedBinding in existingBindings.Except(currentBindings))
                {
                    bindingLogger?.QueueUnbind(queueName, deletedBinding.Exchange, deletedBinding.RoutingKey);
                    channel.QueueUnbind(queueName, deletedBinding.Exchange, deletedBinding.RoutingKey);
                }
            }).ConfigureAwait(false);
        }


        private static IDictionary<string, object>? GetDeclareArguments(IRabbitMQArguments? arguments)
        {
            return arguments == null || arguments.Count == 0 
                ? null 
                : arguments.ToDictionary(p => p.Key, p => p.Value);
        }


        /// <inheritdoc />
        public async Task DurableQueueVerify(string queueName, IRabbitMQArguments? arguments, CancellationToken cancellationToken)
        {
            if (!await GetDurableQueueDeclareRequired(queueName, arguments).ConfigureAwait(false))
                return;

            await defaultConsumeChannel.Queue(channel =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                (logger as IBindingLogger)?.QueueDeclare(queueName, true, true);
                channel.QueueDeclarePassive(queueName);
            }).ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task DurableQueueDelete(string queueName, bool onlyIfEmpty, CancellationToken cancellationToken)
        {
            if (!onlyIfEmpty)
            {
                uint deletedMessages = 0;

                await defaultConsumeChannel.Queue(channel =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    deletedMessages = channel.QueueDelete(queueName);
                }).ConfigureAwait(false);

                deletedQueues.Add(queueName);
                (logger as IBindingLogger)?.QueueObsolete(queueName, true, deletedMessages);
                return;
            }


            await defaultConsumeChannel.QueueWithProvider(async channelProvider =>
            {
                bool retry;
                do
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    retry = false;

                    // Get queue information from the Management API, since the AMQP operations will
                    // throw an error if the queue does not exist or still contains messages and resets
                    // the connection. The resulting reconnect will cause subscribers to reset.
                    var queueInfo = await GetQueueInfo(queueName).ConfigureAwait(false);
                    if (queueInfo == null)
                    {
                        deletedQueues.Add(queueName);
                        return;
                    }

                    if (queueInfo.Messages == 0)
                    {
                        // Still pass onlyIfEmpty to prevent concurrency issues if a message arrived between
                        // the call to the Management API and deleting the queue. Because the QueueWithRetryableChannel
                        // includes the GetQueueInfo, the next time around it should have Messages > 0
                        try
                        {
                            channelProvider.WithChannel(channel =>
                            {
                                channel.QueueDelete(queueName, false, true);
                            });

                            deletedQueues.Add(queueName);
                            (logger as IBindingLogger)?.QueueObsolete(queueName, true, 0);
                        }
                        catch (OperationInterruptedException e)
                        {
                            if (e.ShutdownReason.ReplyCode == Constants.PreconditionFailed)
                                retry = true;
                            else
                                throw;
                        }
                    }
                    else
                    {
                        // Remove all bindings instead
                        var existingBindings = (await GetQueueBindings(queueName).ConfigureAwait(false)).ToList();

                        if (existingBindings.Count > 0)
                        {
                            channelProvider.WithChannel(channel =>
                            {
                                foreach (var binding in existingBindings)
                                    channel.QueueUnbind(queueName, binding.Exchange, binding.RoutingKey);
                            });
                        }

                        (logger as IBindingLogger)?.QueueObsolete(queueName, false, queueInfo.Messages);
                    }
                } while (retry);
            }).ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task<string> DynamicQueueDeclare(string? queuePrefix, IRabbitMQArguments? arguments, CancellationToken cancellationToken)
        {
            string? queueName = null;
            var bindingLogger = logger as IBindingLogger;

            await defaultConsumeChannel.Queue(channel =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (!string.IsNullOrEmpty(queuePrefix))
                {
                    queueName = queuePrefix + "." + Guid.NewGuid().ToString("N");
                    bindingLogger?.QueueDeclare(queueName, false, false);
                    channel.QueueDeclare(queueName, arguments: GetDeclareArguments(arguments));
                }
                else
                {
                    queueName = channel.QueueDeclare(arguments: GetDeclareArguments(arguments)).QueueName;
                    bindingLogger?.QueueDeclare(queueName, false, false);
                }
            }).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (queueName == null)
                throw new InvalidOperationException("Failed to declare dynamic queue");

            return queueName;
        }

        /// <inheritdoc />
        public async Task DynamicQueueBind(string queueName, QueueBinding binding, CancellationToken cancellationToken)
        {
            await defaultConsumeChannel.Queue(channel =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                DeclareExchange(channel, binding.Exchange);
                (logger as IBindingLogger)?.QueueBind(queueName, false, binding.Exchange, binding.RoutingKey);
                channel.QueueBind(queueName, binding.Exchange, binding.RoutingKey);
            }).ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task Close()
        {
            // Empty the queue
            await defaultConsumeChannel.Close().ConfigureAwait(false);
            await defaultPublishChannel.Close().ConfigureAwait(false);

            foreach (var channel in dedicatedChannels)
                await channel.Close().ConfigureAwait(false);

            dedicatedChannels.Clear();
            connection.Close();


            // Wait for message handlers to finish
            await messageHandlerTracker.WaitForIdle(CloseMessageHandlersTimeout);
        }


        private static readonly List<HttpStatusCode> TransientStatusCodes = new()
        {
            HttpStatusCode.GatewayTimeout,
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.ServiceUnavailable
        };


        private class ManagementQueueInfo
        {
            [JsonProperty("name")]
            public string? Name { get; set; }

            [JsonProperty("vhost")]
            public string? VHost { get; set; }

            [JsonProperty("durable")]
            public bool Durable { get; set; }

            [JsonProperty("auto_delete")]
            public bool AutoDelete { get; set; }

            [JsonProperty("exclusive")]
            public bool Exclusive { get; set; }

            [JsonProperty("arguments")]
            public Dictionary<string, JValue>? Arguments { get; set; }

            [JsonProperty("messages")]
            public uint Messages { get; set; }
        }



        private async Task<ManagementQueueInfo?> GetQueueInfo(string queueName)
        {
            var virtualHostPath = Uri.EscapeDataString(connectionParams.VirtualHost);
            var queuePath = Uri.EscapeDataString(queueName);

            return await WithRetryableManagementAPI($"queues/{virtualHostPath}/{queuePath}", async response =>
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<ManagementQueueInfo>(content);
            }).ConfigureAwait(false);
        }


        private class ManagementBinding
        {
            [JsonProperty("source")]
            public string? Source { get; set; }

            [JsonProperty("vhost")]
            public string? Vhost { get; set; }

            [JsonProperty("destination")]
            public string? Destination { get; set; }

            [JsonProperty("destination_type")]
            public string? DestinationType { get; set; }

            [JsonProperty("routing_key")]
            public string? RoutingKey { get; set; }

            [JsonProperty("arguments")]
            public Dictionary<string, string>? Arguments { get; set; }

            [JsonProperty("properties_key")]
            public string? PropertiesKey { get; set; }
        }

        
        private async Task<IEnumerable<QueueBinding>> GetQueueBindings(string queueName)
        {
            var virtualHostPath = Uri.EscapeDataString(connectionParams.VirtualHost);
            var queuePath = Uri.EscapeDataString(queueName);

            return await WithRetryableManagementAPI($"queues/{virtualHostPath}/{queuePath}/bindings", async response =>
            {
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var bindings = JsonConvert.DeserializeObject<IEnumerable<ManagementBinding>>(content);

                // Filter out the binding to an empty source, which is always present for direct-to-queue routing
                return bindings?
                    .Where(binding => !string.IsNullOrEmpty(binding.Source) && !string.IsNullOrEmpty(binding.RoutingKey))
                    .Select(binding => new QueueBinding(binding.Source!, binding.RoutingKey!)) 
                       ?? Enumerable.Empty<QueueBinding>();
            }).ConfigureAwait(false);
        }


        private static readonly TimeSpan[] ExponentialBackoff =
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(13),
            TimeSpan.FromSeconds(21),
            TimeSpan.FromSeconds(34),
            TimeSpan.FromSeconds(55)
        };


        private async Task<T> WithRetryableManagementAPI<T>(string path, Func<HttpResponseMessage, Task<T>> handleResponse)
        {
            // Workaround for: https://github.com/dotnet/runtime/issues/23581#issuecomment-354391321
            // "localhost" can cause a 1 second delay *per call*. Not an issue in production scenarios, but annoying while debugging.
            var hostName = connectionParams.HostName;
            if (hostName.Equals("localhost", StringComparison.InvariantCultureIgnoreCase))
                hostName = "127.0.0.1";
            
            var requestUri = new Uri($"http://{hostName}:{connectionParams.ManagementPort}/api/{path}");

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var retryDelayIndex = 0;

            while (true)
            {
                try
                {
                    var response = await managementClient.SendAsync(request).ConfigureAwait(false);
                    return await handleResponse(response).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                }
                catch (WebException e)
                {
                    if (e.Response is not HttpWebResponse response)
                        throw;

                    if (!TransientStatusCodes.Contains(response.StatusCode))
                        throw;
                }

                await Task.Delay(ExponentialBackoff[retryDelayIndex]).ConfigureAwait(false);

                if (retryDelayIndex < ExponentialBackoff.Length - 1)
                    retryDelayIndex++;
            }
        }


        private readonly HashSet<string> declaredExchanges = new();

        private void DeclareExchange(IModel channel, string exchange)
        {
            if (declaredExchanges.Contains(exchange))
                return;

            (logger as IBindingLogger)?.ExchangeDeclare(exchange);
            channel.ExchangeDeclare(exchange, "topic", true);
            declaredExchanges.Add(exchange);
        }


        private TapetiChannel CreateDedicatedConsumeChannel()
        {
            var channel = connection.CreateChannel(InitConsumeModel);
            dedicatedChannels.Add(channel);

            return channel;
        }


        private void HandleBasicReturn(object? sender, BasicReturnEventArgs e)        
        {
            /*
             * "If the message is also published as mandatory, the basic.return is sent to the client before basic.ack."
             * - https://www.rabbitmq.com/confirms.html
             *
             * Because there is no delivery tag included in the basic.return message. This solution is modeled after
             * user OhJeez' answer on StackOverflow:
             *
             * "Since all messages with the same routing key are routed the same way. I assumed that once I get a
             *  basic.return about a specific routing key, all messages with this routing key can be considered undelivered"
             * https://stackoverflow.com/questions/21336659/how-to-tell-which-amqp-message-was-not-routed-from-basic-return-response
             */
            var key = GetReturnKey(e.Exchange, e.RoutingKey);

            if (!returnRoutingKeys.TryGetValue(key, out var returnInfo))
            {
                returnInfo = new ReturnInfo
                {
                    RefCount = 0,
                    FirstReplyCode = e.ReplyCode
                };

                returnRoutingKeys.Add(key, returnInfo);
            }

            returnInfo.RefCount++;
        }


        private void HandleBasicAck(object? sender, BasicAckEventArgs e)
        {
            Monitor.Enter(confirmLock);
            try
            {
                foreach (var deliveryTag in GetDeliveryTags(e))
                {
                    if (!confirmMessages.TryGetValue(deliveryTag, out var messageInfo))
                        continue;

                    if (returnRoutingKeys.TryGetValue(messageInfo.ReturnKey, out var returnInfo))
                    {
                        messageInfo.CompletionSource.SetResult(returnInfo.FirstReplyCode);

                        returnInfo.RefCount--;
                        if (returnInfo.RefCount == 0)
                            returnRoutingKeys.Remove(messageInfo.ReturnKey);
                    }
                    else
                        messageInfo.CompletionSource.SetResult(0);

                    confirmMessages.Remove(deliveryTag);
                }
            }
            finally
            {
                Monitor.Exit(confirmLock);
            }
        }


        private void HandleBasicNack(object? sender, BasicNackEventArgs e)
        {
            Monitor.Enter(confirmLock);
            try
            {
                foreach (var deliveryTag in GetDeliveryTags(e))
                {
                    if (!confirmMessages.TryGetValue(deliveryTag, out var messageInfo)) 
                        continue;

                    messageInfo.CompletionSource.SetCanceled();
                    confirmMessages.Remove(e.DeliveryTag);
                }
            }
            finally
            {
                Monitor.Exit(confirmLock);
            }
        }


        private IEnumerable<ulong> GetDeliveryTags(BasicAckEventArgs e)
        {
            return e.Multiple
                ? confirmMessages.Keys.Where(tag => tag <= e.DeliveryTag).ToArray()
                : new[] { e.DeliveryTag };
        }


        private IEnumerable<ulong> GetDeliveryTags(BasicNackEventArgs e)
        {
            return e.Multiple
                ? confirmMessages.Keys.Where(tag => tag <= e.DeliveryTag).ToArray()
                : new[] { e.DeliveryTag };
        }


        private static string GetReturnKey(string exchange, string routingKey)
        {
            return exchange + ':' + routingKey;
        }


        private class TapetiConsumerTag : ITapetiConsumerTag
        {
            private readonly TapetiClient client;
            private readonly TapetiChannel channel;

            public string ConsumerTag { get; }
            public long ConnectionReference { get; }


            public TapetiConsumerTag(TapetiClient client, TapetiChannel channel, string consumerTag, long connectionReference)
            {
                this.client = client;
                this.channel = channel;

                ConnectionReference = connectionReference;
                ConsumerTag = consumerTag;
            }


            public Task Cancel()
            {
                return client.Cancel(channel, ConsumerTag, ConnectionReference);
            }
        }

    }
}
