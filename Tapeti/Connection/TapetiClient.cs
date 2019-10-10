using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Tapeti.Config;
using Tapeti.Default;
using Tapeti.Exceptions;
using Tapeti.Tasks;

namespace Tapeti.Connection
{
    /// <inheritdoc />
    /// <summary>
    /// Implementation of ITapetiClient for the RabbitMQ Client library
    /// </summary>
    internal class TapetiClient : ITapetiClient
    {
        private const int ReconnectDelay = 5000;
        private const int MandatoryReturnTimeout = 30000;
        private const int MinimumConnectedReconnectDelay = 1000;

        private readonly TapetiConnectionParams connectionParams;

        private readonly ITapetiConfig config;
        private readonly ILogger logger;


        /// <summary>
        /// Receives events when the connection state changes.
        /// </summary>
        public IConnectionEventListener ConnectionEventListener { get; set; }


        private readonly Lazy<SingleThreadTaskQueue> taskQueue = new Lazy<SingleThreadTaskQueue>();

        
        // These fields are for use in the taskQueue only!
        private RabbitMQ.Client.IConnection connection;
        private bool isClosing;
        private bool isReconnect;
        private IModel channelInstance;
        private ulong lastDeliveryTag;
        private DateTime connectedDateTime;
        private readonly HttpClient managementClient;
        private readonly HashSet<string> deletedQueues = new HashSet<string>();

        // These fields must be locked, since the callbacks for BasicAck/BasicReturn can run in a different thread
        private readonly object confirmLock = new object();
        private readonly Dictionary<ulong, ConfirmMessageInfo> confirmMessages = new Dictionary<ulong, ConfirmMessageInfo>();
        private readonly Dictionary<string, ReturnInfo> returnRoutingKeys = new Dictionary<string, ReturnInfo>();


        private class ConfirmMessageInfo
        {
            public string ReturnKey;
            public TaskCompletionSource<int> CompletionSource;
        }


        private class ReturnInfo
        {
            public uint RefCount;
            public int FirstReplyCode;
        }


        /// <inheritdoc />
        public TapetiClient(ITapetiConfig config, TapetiConnectionParams connectionParams)
        {
            this.config = config;
            this.connectionParams = connectionParams;

            logger = config.DependencyResolver.Resolve<ILogger>();


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


        /// <inheritdoc />
        public async Task Publish(byte[] body, IMessageProperties properties, string exchange, string routingKey, bool mandatory)
        {
            if (string.IsNullOrEmpty(routingKey))
                throw new ArgumentNullException(nameof(routingKey));

            await taskQueue.Value.Add(async () =>
            {
                Task<int> publishResultTask = null;
                var messageInfo = new ConfirmMessageInfo
                {
                    ReturnKey = GetReturnKey(exchange, routingKey),
                    CompletionSource = new TaskCompletionSource<int>()
                };


                WithRetryableChannel(channel =>
                {
                    DeclareExchange(channel, exchange);

                    // The delivery tag is lost after a reconnect, register under the new tag
                    if (config.Features.PublisherConfirms)
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
                        channel.BasicPublish(exchange ?? "", routingKey, mandatory, publishProperties.BasicProperties, body);
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
                    Task.Delay(MandatoryReturnTimeout, delayCancellationTokenSource.Token));

                if (signalledTask != publishResultTask)
                    throw new TimeoutException(
                        $"Timeout while waiting for basic.return for message with exchange '{exchange}' and routing key '{routingKey}'");

                delayCancellationTokenSource.Cancel();

                if (publishResultTask.IsCanceled)
                    throw new NackException(
                        $"Mandatory message with with exchange '{exchange}' and routing key '{routingKey}' was nacked");

                var replyCode = publishResultTask.Result;

                // There is no RabbitMQ.Client.Framing.Constants value for this "No route" reply code
                // at the time of writing...
                if (replyCode == 312)
                    throw new NoRouteException(
                        $"Mandatory message with exchange '{exchange}' and routing key '{routingKey}' does not have a route");

                if (replyCode > 0)
                    throw new NoRouteException(
                        $"Mandatory message with exchange '{exchange}' and routing key '{routingKey}' could not be delivered, reply code: {replyCode}");
            });
        }


        /// <inheritdoc />
        public async Task Consume(CancellationToken cancellationToken, string queueName, IConsumer consumer)
        {
            if (deletedQueues.Contains(queueName))
                return;

            if (string.IsNullOrEmpty(queueName))
                throw new ArgumentNullException(nameof(queueName));


            await QueueWithRetryableChannel(channel =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var basicConsumer = new TapetiBasicConsumer(consumer, Respond);
                channel.BasicConsume(queueName, false, basicConsumer);
            });
        }


        private async Task Respond(ulong deliveryTag, ConsumeResult result)
        {
            await taskQueue.Value.Add(() =>
            {
                // No need for a retryable channel here, if the connection is lost we can't
                // use the deliveryTag anymore.
                switch (result)
                {
                    case ConsumeResult.Success:
                    case ConsumeResult.ExternalRequeue:
                        GetChannel().BasicAck(deliveryTag, false);
                        break;

                    case ConsumeResult.Error:
                        GetChannel().BasicNack(deliveryTag, false, false);
                        break;

                    case ConsumeResult.Requeue:
                        GetChannel().BasicNack(deliveryTag, false, true);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(result), result, null);
                }
            });
        }


        /// <inheritdoc />
        public async Task DurableQueueDeclare(CancellationToken cancellationToken, string queueName, IEnumerable<QueueBinding> bindings)
        {
            var existingBindings = (await GetQueueBindings(queueName)).ToList();
            var currentBindings = bindings.ToList();

            await Queue(channel =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                channel.QueueDeclare(queueName, true, false, false);

                foreach (var binding in currentBindings.Except(existingBindings))
                {
                    DeclareExchange(channel, binding.Exchange);
                    channel.QueueBind(queueName, binding.Exchange, binding.RoutingKey);
                }

                foreach (var deletedBinding in existingBindings.Except(currentBindings))
                    channel.QueueUnbind(queueName, deletedBinding.Exchange, deletedBinding.RoutingKey);
            });
        }

        /// <inheritdoc />
        public async Task DurableQueueVerify(CancellationToken cancellationToken, string queueName)
        {
            await Queue(channel =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                channel.QueueDeclarePassive(queueName);
            });
        }


        /// <inheritdoc />
        public async Task DurableQueueDelete(CancellationToken cancellationToken, string queueName, bool onlyIfEmpty = true)
        {
            if (!onlyIfEmpty)
            {
                uint deletedMessages = 0;

                await Queue(channel =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    deletedMessages = channel.QueueDelete(queueName);
                });

                deletedQueues.Add(queueName);
                logger.QueueObsolete(queueName, true, deletedMessages);
                return;
            }


            await taskQueue.Value.Add(async () =>
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
                    var queueInfo = await GetQueueInfo(queueName);
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
                            var channel = GetChannel();
                            channel.QueueDelete(queueName, false, true);

                            deletedQueues.Add(queueName);
                            logger.QueueObsolete(queueName, true, 0);
                        }
                        catch (OperationInterruptedException e)
                        {
                            if (e.ShutdownReason.ReplyCode == RabbitMQ.Client.Framing.Constants.PreconditionFailed)
                                retry = true;
                            else
                                throw;
                        }
                    }
                    else
                    {
                        // Remove all bindings instead
                        var existingBindings = (await GetQueueBindings(queueName)).ToList();

                        if (existingBindings.Count > 0)
                        {
                            var channel = GetChannel();

                            foreach (var binding in existingBindings)
                                channel.QueueUnbind(queueName, binding.Exchange, binding.RoutingKey);
                        }

                        logger.QueueObsolete(queueName, false, queueInfo.Messages);
                    }
                } while (retry);
            });
        }


        /// <inheritdoc />
        public async Task<string> DynamicQueueDeclare(CancellationToken cancellationToken, string queuePrefix = null)
        {
            string queueName = null;

            await Queue(channel =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (!string.IsNullOrEmpty(queuePrefix))
                {
                    queueName = queuePrefix + "." + Guid.NewGuid().ToString("N");
                    channel.QueueDeclare(queueName);
                }
                else
                    queueName = channel.QueueDeclare().QueueName;
            });

            return queueName;
        }

        /// <inheritdoc />
        public async Task DynamicQueueBind(CancellationToken cancellationToken, string queueName, QueueBinding binding)
        {
            await Queue(channel =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                DeclareExchange(channel, binding.Exchange); 
                channel.QueueBind(queueName, binding.Exchange, binding.RoutingKey);                    
            });
        }


        /// <inheritdoc />
        public async Task Close()
        {
            if (!taskQueue.IsValueCreated)
                return;

            await taskQueue.Value.Add(() =>
            {
                isClosing = true;

                if (channelInstance != null)
                {
                    channelInstance.Dispose();
                    channelInstance = null;
                }

                // ReSharper disable once InvertIf
                if (connection != null)
                {
                    connection.Dispose();
                    connection = null;
                }

                taskQueue.Value.Dispose();
            });
        }


        private static readonly List<HttpStatusCode> TransientStatusCodes = new List<HttpStatusCode>
        {
            HttpStatusCode.GatewayTimeout,
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.ServiceUnavailable
        };


        private class ManagementQueueInfo
        {
            [JsonProperty("messages")]
            public uint Messages { get; set; }
        }



        private async Task<ManagementQueueInfo> GetQueueInfo(string queueName)
        {
            var virtualHostPath = Uri.EscapeDataString(connectionParams.VirtualHost);
            var queuePath = Uri.EscapeDataString(queueName);

            return await WithRetryableManagementAPI($"queues/{virtualHostPath}/{queuePath}", async response =>
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ManagementQueueInfo>(content);
            });
        }


        private class ManagementBinding
        {
            [JsonProperty("source")]
            public string Source { get; set; }

            [JsonProperty("vhost")]
            public string Vhost { get; set; }

            [JsonProperty("destination")]
            public string Destination { get; set; }

            [JsonProperty("destination_type")]
            public string DestinationType { get; set; }

            [JsonProperty("routing_key")]
            public string RoutingKey { get; set; }

            [JsonProperty("arguments")]
            public Dictionary<string, string> Arguments { get; set; }

            [JsonProperty("properties_key")]
            public string PropertiesKey { get; set; }
        }


        private async Task<IEnumerable<QueueBinding>> GetQueueBindings(string queueName)
        {
            var virtualHostPath = Uri.EscapeDataString(connectionParams.VirtualHost);
            var queuePath = Uri.EscapeDataString(queueName);

            return await WithRetryableManagementAPI($"queues/{virtualHostPath}/{queuePath}/bindings", async response =>
            {
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var bindings = JsonConvert.DeserializeObject<IEnumerable<ManagementBinding>>(content);

                // Filter out the binding to an empty source, which is always present for direct-to-queue routing
                return bindings
                    .Where(binding => !string.IsNullOrEmpty(binding.Source))
                    .Select(binding => new QueueBinding(binding.Source, binding.RoutingKey));
            });
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
            var requestUri = new Uri($"http://{connectionParams.HostName}:{connectionParams.ManagementPort}/api/{path}");

            using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
            {
                var retryDelayIndex = 0;

                while (true)
                {
                    try
                    {
                        var response = await managementClient.SendAsync(request);
                        return await handleResponse(response);
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (WebException e)
                    {
                        if (!(e.Response is HttpWebResponse response))
                            throw;

                        if (!TransientStatusCodes.Contains(response.StatusCode))
                            throw;
                    }

                    await Task.Delay(ExponentialBackoff[retryDelayIndex]);

                    if (retryDelayIndex < ExponentialBackoff.Length - 1)
                        retryDelayIndex++;
                }
            }
        }


        private readonly HashSet<string> declaredExchanges = new HashSet<string>();

        private void DeclareExchange(IModel channel, string exchange)
        {
            if (string.IsNullOrEmpty(exchange))
                return;

            if (declaredExchanges.Contains(exchange))
                return;

            channel.ExchangeDeclare(exchange, "topic", true);
            declaredExchanges.Add(exchange);
        }


        private async Task Queue(Action<IModel> operation)
        {
            await taskQueue.Value.Add(() =>
            {
                var channel = GetChannel();
                operation(channel);
            });
        }


        private async Task QueueWithRetryableChannel(Action<IModel> operation)
        {
            await taskQueue.Value.Add(() =>
            {
                WithRetryableChannel(operation);
            });
        }


        /// <remarks>
        /// Only call this from a task in the taskQueue to ensure IModel is only used 
        /// by a single thread, as is recommended in the RabbitMQ .NET Client documentation.
        /// </remarks>
        private void WithRetryableChannel(Action<IModel> operation)
        {
            while (true)
            {
                try
                {
                    operation(GetChannel());
                    break;
                }
                catch (AlreadyClosedException)
                {
                }
            }
        }


        /// <remarks>
        /// Only call this from a task in the taskQueue to ensure IModel is only used 
        /// by a single thread, as is recommended in the RabbitMQ .NET Client documentation.
        /// </remarks>
        private IModel GetChannel()
        {
            if (channelInstance != null && channelInstance.IsOpen)
                return channelInstance;

            // If the Disconnect quickly follows the Connect (when an error occurs that is reported back by RabbitMQ
            // not related to the connection), wait for a bit to avoid spamming the connection
            if ((DateTime.UtcNow - connectedDateTime).TotalMilliseconds <= MinimumConnectedReconnectDelay)
                Thread.Sleep(ReconnectDelay);


            var connectionFactory = new ConnectionFactory
            {
                HostName = connectionParams.HostName,
                Port = connectionParams.Port,
                VirtualHost = connectionParams.VirtualHost,
                UserName = connectionParams.Username,
                Password = connectionParams.Password,
                AutomaticRecoveryEnabled = false,
                TopologyRecoveryEnabled = false,
                RequestedHeartbeat = 30
            };

            if (connectionParams.ClientProperties != null)
                foreach (var pair in connectionParams.ClientProperties)
                {
                    if (connectionFactory.ClientProperties.ContainsKey(pair.Key))
                        connectionFactory.ClientProperties[pair.Key] = Encoding.UTF8.GetBytes(pair.Value);
                    else
                        connectionFactory.ClientProperties.Add(pair.Key, Encoding.UTF8.GetBytes(pair.Value));
                }


            while (true)
            {
                try
                {
                    logger.Connect(new ConnectContext(connectionParams, isReconnect));

                    connection = connectionFactory.CreateConnection();
                    channelInstance = connection.CreateModel();

                    if (channelInstance == null)
                        throw new BrokerUnreachableException(null);

                    if (config.Features.PublisherConfirms)
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

                        channelInstance.ConfirmSelect();
                    }

                    if (connectionParams.PrefetchCount > 0)
                        channelInstance.BasicQos(0, connectionParams.PrefetchCount, false);

                    channelInstance.ModelShutdown += (sender, e) =>
                    {
                        ConnectionEventListener?.Disconnected(new DisconnectedEventArgs
                        {
                            ReplyCode = e.ReplyCode,
                            ReplyText = e.ReplyText
                        });

                        logger.Disconnect(new DisconnectContext(connectionParams, e.ReplyCode, e.ReplyText));

                        channelInstance = null;

                        if (!isClosing)
                            taskQueue.Value.Add(() => WithRetryableChannel(channel => { }));
                    };

                    channelInstance.BasicReturn += HandleBasicReturn;
                    channelInstance.BasicAcks += HandleBasicAck;
                    channelInstance.BasicNacks += HandleBasicNack;

                    connectedDateTime = DateTime.UtcNow;

                    var connectedEventArgs = new ConnectedEventArgs
                    {
                        ConnectionParams = connectionParams,
                        LocalPort = connection.LocalPort
                    };

                    if (isReconnect)
                        ConnectionEventListener?.Reconnected(connectedEventArgs);
                    else
                        ConnectionEventListener?.Connected(connectedEventArgs);

                    logger.ConnectSuccess(new ConnectContext(connectionParams, isReconnect, connection.LocalPort));
                    isReconnect = true;

                    break;
                }
                catch (BrokerUnreachableException e)
                {
                    logger.ConnectFailed(new ConnectContext(connectionParams, isReconnect, exception: e));
                    Thread.Sleep(ReconnectDelay);
                }
            }

            return channelInstance;
        }


        private void HandleBasicReturn(object sender, BasicReturnEventArgs e)        
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


        private void HandleBasicAck(object sender, BasicAckEventArgs e)
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

                    messageInfo.CompletionSource.SetResult(0);
                    confirmMessages.Remove(deliveryTag);
                }
            }
            finally
            {
                Monitor.Exit(confirmLock);
            }
        }


        private void HandleBasicNack(object sender, BasicNackEventArgs e)
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



        private class ConnectContext : IConnectSuccessContext, IConnectFailedContext
        {
            public TapetiConnectionParams ConnectionParams { get; }
            public bool IsReconnect { get; }
            public int LocalPort { get; }
            public Exception Exception { get; }


            public ConnectContext(TapetiConnectionParams connectionParams, bool isReconnect, int localPort = 0, Exception exception = null)
            {
                ConnectionParams = connectionParams;
                IsReconnect = isReconnect;
                LocalPort = localPort;
                Exception = exception;
            }
        }


        private class DisconnectContext : IDisconnectContext
        {
            public TapetiConnectionParams ConnectionParams { get; }
            public ushort ReplyCode { get; }
            public string ReplyText { get; }


            public DisconnectContext(TapetiConnectionParams connectionParams, ushort replyCode, string replyText)
            {
                ConnectionParams = connectionParams;
                ReplyCode = replyCode;
                ReplyText = replyText;
            }
        }
    }
}
