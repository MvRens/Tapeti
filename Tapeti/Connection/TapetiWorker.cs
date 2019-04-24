using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using Tapeti.Config;
using Tapeti.Exceptions;
using Tapeti.Helpers;
using Tapeti.Tasks;

namespace Tapeti.Connection
{
    public class TapetiWorker
    {
        private const int ReconnectDelay = 5000;
        private const int MandatoryReturnTimeout = 30000;
        private const int MinimumConnectedReconnectDelay = 1000;

        private readonly IConfig config;
        private readonly ILogger logger;
        public TapetiConnectionParams ConnectionParams { get; set; }
        public IConnectionEventListener ConnectionEventListener { get; set; }

        private readonly IMessageSerializer messageSerializer;
        private readonly IRoutingKeyStrategy routingKeyStrategy;
        private readonly IExchangeStrategy exchangeStrategy;
        private readonly Lazy<SingleThreadTaskQueue> taskQueue = new Lazy<SingleThreadTaskQueue>();

        
        // These fields are for use in the taskQueue only!
        private RabbitMQ.Client.IConnection connection;
        private bool isReconnect;
        private IModel channelInstance;
        private ulong lastDeliveryTag;
        private DateTime connectedDateTime;
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



        public TapetiWorker(IConfig config)
        {
            this.config = config;

            logger = config.DependencyResolver.Resolve<ILogger>();
            messageSerializer = config.DependencyResolver.Resolve<IMessageSerializer>();
            routingKeyStrategy = config.DependencyResolver.Resolve<IRoutingKeyStrategy>();
            exchangeStrategy = config.DependencyResolver.Resolve<IExchangeStrategy>();
        }


        public Task Publish(object message, IBasicProperties properties, bool mandatory)
        {
            return Publish(message, properties, exchangeStrategy.GetExchange(message.GetType()), routingKeyStrategy.GetRoutingKey(message.GetType()), mandatory);
        }


        public Task PublishDirect(object message, string queueName, IBasicProperties properties, bool mandatory)
        {
            return Publish(message, properties, "", queueName, mandatory);
        }


        public Task Consume(string queueName, IEnumerable<IBinding> bindings)
        {
            if (string.IsNullOrEmpty(queueName))
                throw new ArgumentNullException(nameof(queueName));

            return taskQueue.Value.Add(() =>
            {
                WithRetryableChannel(channel => channel.BasicConsume(queueName, false, new TapetiConsumer(this, queueName, config.DependencyResolver, bindings, config.MessageMiddleware, config.CleanupMiddleware)));
            });
        }


        public Task Subscribe(IQueue queue)
        {
            return taskQueue.Value.Add(() =>
            {
                WithRetryableChannel(channel => 
                {
                    if (queue.Dynamic)
                    {
                        if (!(queue is IDynamicQueue dynamicQueue))
                            throw new NullReferenceException("Queue with Dynamic = true must implement IDynamicQueue");

                        var declaredQueue = channel.QueueDeclare(dynamicQueue.GetDeclareQueueName());
                        dynamicQueue.SetName(declaredQueue.QueueName);

                        foreach (var binding in queue.Bindings)
                        {
                            if (binding.QueueBindingMode == QueueBindingMode.RoutingKey)
                            {
                                if (binding.MessageClass == null)
                                    throw new NullReferenceException("Binding with QueueBindingMode = RoutingKey must have a MessageClass");

                                var routingKey = routingKeyStrategy.GetRoutingKey(binding.MessageClass);
                                var exchange = exchangeStrategy.GetExchange(binding.MessageClass);

                                channel.QueueBind(declaredQueue.QueueName, exchange, routingKey);
                            }

                            (binding as IBuildBinding)?.SetQueueName(declaredQueue.QueueName);
                        }
                    }
                    else
                    {
                        channel.QueueDeclarePassive(queue.Name);
                        foreach (var binding in queue.Bindings)
                        {
                            (binding as IBuildBinding)?.SetQueueName(queue.Name);
                        }
                    }
                });
            });
        }


        public Task Respond(ulong deliveryTag, ConsumeResponse response)
        {
            return taskQueue.Value.Add(() =>
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


        public Task Close()
        {
            if (!taskQueue.IsValueCreated)
                return Task.CompletedTask;

            return taskQueue.Value.Add(() =>
            {
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


        private Task Publish(object message, IBasicProperties properties, string exchange, string routingKey, bool mandatory)
        {
            var context = new PublishContext
            {
                DependencyResolver = config.DependencyResolver,
                Exchange = exchange,
                RoutingKey = routingKey,
                Message = message,
                Properties = properties ?? new BasicProperties()
            };

            if (!context.Properties.IsTimestampPresent())
                context.Properties.Timestamp = new AmqpTimestamp(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());

            if (!context.Properties.IsDeliveryModePresent())
                context.Properties.DeliveryMode = 2; // Persistent


            // ReSharper disable ImplicitlyCapturedClosure - MiddlewareHelper will not keep a reference to the lambdas
            return MiddlewareHelper.GoAsync(
                config.PublishMiddleware,
                async (handler, next) => await handler.Handle(context, next),
                () => taskQueue.Value.Add(async () =>
                {
                    var body = messageSerializer.Serialize(context.Message, context.Properties);

                    Task<int> publishResultTask = null;
                    var messageInfo = new ConfirmMessageInfo
                    {
                        ReturnKey = GetReturnKey(context.Exchange, context.RoutingKey),
                        CompletionSource = new TaskCompletionSource<int>()
                    };


                    WithRetryableChannel(channel =>
                    {
                        // The delivery tag is lost after a reconnect, register under the new tag
                        if (config.UsePublisherConfirms)
                        {
                            lastDeliveryTag++;

                            confirmMessages.Add(lastDeliveryTag, messageInfo);
                            publishResultTask = messageInfo.CompletionSource.Task;
                        }
                        else
                            mandatory = false;

                        channel.BasicPublish(context.Exchange, context.RoutingKey, mandatory, context.Properties, body);
                    });


                    if (publishResultTask == null)
                        return;

                    var delayCancellationTokenSource = new CancellationTokenSource();
                    var signalledTask = await Task.WhenAny(publishResultTask, Task.Delay(MandatoryReturnTimeout, delayCancellationTokenSource.Token));

                    if (signalledTask != publishResultTask)
                        throw new TimeoutException($"Timeout while waiting for basic.return for message with class {context.Message?.GetType().FullName ?? "null"} and Id {context.Properties.MessageId}");

                    delayCancellationTokenSource.Cancel();

                    if (publishResultTask.IsCanceled)
                        throw new NackException($"Mandatory message with class {context.Message?.GetType().FullName ?? "null"} was nacked");

                    var replyCode = publishResultTask.Result;

                    // There is no RabbitMQ.Client.Framing.Constants value for this "No route" reply code
                    // at the time of writing...
                    if (replyCode == 312)
                        throw new NoRouteException($"Mandatory message with class {context.Message?.GetType().FullName ?? "null"} does not have a route");

                    if (replyCode > 0)
                        throw new NoRouteException($"Mandatory message with class {context.Message?.GetType().FullName ?? "null"} could not be delivery, reply code {replyCode}");
                }));
            // ReSharper restore ImplicitlyCapturedClosure
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
                catch (AlreadyClosedException e)
                {
                    // TODO log?                    
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
                HostName = ConnectionParams.HostName,
                Port = ConnectionParams.Port,
                VirtualHost = ConnectionParams.VirtualHost,
                UserName = ConnectionParams.Username,
                Password = ConnectionParams.Password,
                AutomaticRecoveryEnabled = false,
                TopologyRecoveryEnabled = false,
                RequestedHeartbeat = 30
            };

            while (true)
            {
                try
                {
                    logger.Connect(ConnectionParams);

                    connection = connectionFactory.CreateConnection();
                    channelInstance = connection.CreateModel();

                    if (channelInstance == null)
                        throw new BrokerUnreachableException(null);

                    if (config.UsePublisherConfirms)
                    {
                        lastDeliveryTag = 0;
                        confirmMessages.Clear();
                        channelInstance.ConfirmSelect();
                    }

                    if (ConnectionParams.PrefetchCount > 0)
                        channelInstance.BasicQos(0, ConnectionParams.PrefetchCount, false);

                    channelInstance.ModelShutdown += (sender, e) =>
                    {
                        ConnectionEventListener?.Disconnected(new DisconnectedEventArgs
                        {
                            ReplyCode = e.ReplyCode,
                            ReplyText = e.ReplyText
                        });

                        channelInstance = null;
                    };

                    channelInstance.BasicReturn += HandleBasicReturn;
                    channelInstance.BasicAcks += HandleBasicAck;
                    channelInstance.BasicNacks += HandleBasicNack;

                    connectedDateTime = DateTime.UtcNow;

                    if (isReconnect)
                        ConnectionEventListener?.Reconnected();
                    else
                        ConnectionEventListener?.Connected();

                    logger.ConnectSuccess(ConnectionParams);
                    isReconnect = true;

                    break;
                }
                catch (BrokerUnreachableException e)
                {
                    logger.ConnectFailed(ConnectionParams, e);
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


        private void HandleBasicNack(object sender, BasicNackEventArgs e)
        {
            foreach (var deliveryTag in GetDeliveryTags(e))
            {
                if (!confirmMessages.TryGetValue(deliveryTag, out var messageInfo)) 
                    continue;

                messageInfo.CompletionSource.SetCanceled();
                confirmMessages.Remove(e.DeliveryTag);
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


        private class PublishContext : IPublishContext
        {
            public IDependencyResolver DependencyResolver { get; set; }
            public string Exchange { get; set; }
            public string RoutingKey { get; set; }
            public object Message { get; set; }
            public IBasicProperties Properties { get; set; }
        }
    }
}
