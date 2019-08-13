using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
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
    public class TapetiClient : ITapetiClient
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
        private HttpClient managementClient;

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
            var publishProperties = new RabbitMQMessageProperties(new BasicProperties(), properties);

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

                    channel.BasicPublish(exchange, routingKey, mandatory, publishProperties.BasicProperties, body);
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
        public async Task Consume(string queueName, IConsumer consumer)
        {
            if (string.IsNullOrEmpty(queueName))
                throw new ArgumentNullException(nameof(queueName));

            await taskQueue.Value.Add(() =>
            {
                WithRetryableChannel(channel =>
                {
                    var basicConsumer = new TapetiBasicConsumer(consumer, Respond);
                    channel.BasicConsume(queueName, false, basicConsumer);
                });
            });
        }


        private async Task Respond(ulong deliveryTag, ConsumeResponse response)
        {
            await taskQueue.Value.Add(() =>
            {
                // No need for a retryable channel here, if the connection is lost we can't
                // use the deliveryTag anymore.
                switch (response)
                {
                    case ConsumeResponse.Ack:
                        GetChannel().BasicAck(deliveryTag, false);
                        break;

                    case ConsumeResponse.Nack:
                        GetChannel().BasicNack(deliveryTag, false, false);
                        break;

                    case ConsumeResponse.Requeue:
                        GetChannel().BasicNack(deliveryTag, false, true);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(response), response, null);
                }

            });
        }


        /// <inheritdoc />
        public async Task DurableQueueDeclare(string queueName, IEnumerable<QueueBinding> bindings)
        {
            await taskQueue.Value.Add(async () =>
            {
                var existingBindings = await GetQueueBindings(queueName);

                WithRetryableChannel(channel =>
                {
                    channel.QueueDeclare(queueName, true, false, false);

                    var currentBindings = bindings.ToList();

                    foreach (var binding in currentBindings)
                        channel.QueueBind(queueName, binding.Exchange, binding.RoutingKey);

                    foreach (var deletedBinding in existingBindings.Where(binding => !currentBindings.Any(b => b.Exchange == binding.Exchange && b.RoutingKey == binding.RoutingKey)))
                        channel.QueueUnbind(queueName, deletedBinding.Exchange, deletedBinding.RoutingKey);
                });
            });
        }

        /// <inheritdoc />
        public async Task DurableQueueVerify(string queueName)
        {
            await taskQueue.Value.Add(() => 
            { 
                WithRetryableChannel(channel =>
                {
                    channel.QueueDeclarePassive(queueName);
                });
            });
        }

        /// <inheritdoc />
        public async Task<string> DynamicQueueDeclare(string queuePrefix = null)
        {
            string queueName = null;

            await taskQueue.Value.Add(() =>
            {
                WithRetryableChannel(channel =>
                {
                    if (!string.IsNullOrEmpty(queuePrefix))
                    {
                        queueName = queuePrefix + "." + Guid.NewGuid().ToString("N");
                        channel.QueueDeclare(queueName);
                    }
                    else
                        queueName = channel.QueueDeclare().QueueName;
                });
            });

            return queueName;
        }

        /// <inheritdoc />
        public async Task DynamicQueueBind(string queueName, QueueBinding binding)
        {
            await taskQueue.Value.Add(() =>
            {
                WithRetryableChannel(channel =>
                {
                    channel.QueueBind(queueName, binding.Exchange, binding.RoutingKey);                    
                });
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
            var requestUri = new Uri($"{connectionParams.HostName}:{connectionParams.Port}/api/queues/{virtualHostPath}/{queuePath}/bindings");

            using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
            { 
                var retryDelayIndex = 0;

                while (true)
                {
                    try
                    {
                        var response = await managementClient.SendAsync(request);
                        response.EnsureSuccessStatusCode();

                        var content = await response.Content.ReadAsStringAsync();
                        var bindings = JsonConvert.DeserializeObject<IEnumerable<ManagementBinding>>(content);

                        // Filter out the binding to an empty source, which is always present for direct-to-queue routing
                        return bindings
                            .Where(binding => !string.IsNullOrEmpty(binding.Source))
                            .Select(binding => new QueueBinding(binding.Source, binding.RoutingKey));
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

            while (true)
            {
                try
                {
                    logger.Connect(connectionParams);

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

                        channelInstance = null;

                        if (!isClosing)
                            taskQueue.Value.Add(() => WithRetryableChannel(channel => { }));
                    };

                    channelInstance.BasicReturn += HandleBasicReturn;
                    channelInstance.BasicAcks += HandleBasicAck;
                    channelInstance.BasicNacks += HandleBasicNack;

                    connectedDateTime = DateTime.UtcNow;

                    if (isReconnect)
                        ConnectionEventListener?.Reconnected();
                    else
                        ConnectionEventListener?.Connected();

                    logger.ConnectSuccess(connectionParams);
                    isReconnect = true;

                    break;
                }
                catch (BrokerUnreachableException e)
                {
                    logger.ConnectFailed(connectionParams, e);
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
    }
}
