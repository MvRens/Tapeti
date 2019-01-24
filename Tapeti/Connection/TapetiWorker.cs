using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
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
        private const int PublishMaxConnectAttempts = 3;

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
        private IModel channelInstance;
        private TaskCompletionSource<int> publishResultTaskSource;


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

            return taskQueue.Value.Add(async () =>
            {
                (await GetChannel()).BasicConsume(queueName, false, new TapetiConsumer(this, queueName, config.DependencyResolver, bindings, config.MessageMiddleware, config.CleanupMiddleware));
            }).Unwrap();
        }


        public Task Subscribe(IQueue queue)
        {
            return taskQueue.Value.Add(async () =>
            {
                var channel = await GetChannel();

                if (queue.Dynamic)
                {
                    var dynamicQueue = channel.QueueDeclare(queue.Name);
                    (queue as IDynamicQueue)?.SetName(dynamicQueue.QueueName);

                    foreach (var binding in queue.Bindings)
                    {
                        if (binding.QueueBindingMode == QueueBindingMode.RoutingKey)
                        {
                            var routingKey = routingKeyStrategy.GetRoutingKey(binding.MessageClass);
                            var exchange = exchangeStrategy.GetExchange(binding.MessageClass);

                            channel.QueueBind(dynamicQueue.QueueName, exchange, routingKey);
                        }

                        (binding as IBuildBinding)?.SetQueueName(dynamicQueue.QueueName);
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
            }).Unwrap();
        }


        public Task Respond(ulong deliveryTag, ConsumeResponse response)
        {
            return taskQueue.Value.Add(async () =>
            {
                switch (response)
                {
                    case ConsumeResponse.Ack:
                        (await GetChannel()).BasicAck(deliveryTag, false);
                        break;

                    case ConsumeResponse.Nack:
                        (await GetChannel()).BasicNack(deliveryTag, false, false);
                        break;

                    case ConsumeResponse.Requeue:
                        (await GetChannel()).BasicNack(deliveryTag, false, true);
                        break;
                }

            }).Unwrap();
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

                    if (config.UsePublisherConfirms)
                    {
                        publishResultTaskSource = new TaskCompletionSource<int>();
                        publishResultTask = publishResultTaskSource.Task;
                    }
                    else
                        mandatory = false;

                    (await GetChannel(PublishMaxConnectAttempts)).BasicPublish(context.Exchange, context.RoutingKey, mandatory, context.Properties, body);

                    if (publishResultTask != null)
                    {
                        var timerCancellationSource = new CancellationTokenSource();

                        if (await Task.WhenAny(publishResultTask, Task.Delay(MandatoryReturnTimeout, timerCancellationSource.Token)) == publishResultTask)
                        {
                            timerCancellationSource.Cancel();

                            var replyCode = publishResultTask.Result;

                            // There is no RabbitMQ.Client.Framing.Constants value for this "No route" reply code
                            // at the time of writing...
                            if (replyCode == 312)
                                throw new NoRouteException($"Mandatory message with class {context.Message?.GetType().FullName ?? "null"} does not have a route");

                            if (replyCode > 0)
                                throw new NoRouteException($"Mandatory message with class {context.Message?.GetType().FullName ?? "null"} could not be delivery, reply code {replyCode}");
                        }
                        else
                            throw new TimeoutException($"Timeout while waiting for basic.return for message with class {context.Message?.GetType().FullName ?? "null"} and Id {context.Properties.MessageId}");
                    }
                }).Unwrap());
            // ReSharper restore ImplicitlyCapturedClosure
        }

        /// <remarks>
        /// Only call this from a task in the taskQueue to ensure IModel is only used 
        /// by a single thread, as is recommended in the RabbitMQ .NET Client documentation.
        /// </remarks>
        private async Task<IModel> GetChannel(int? maxAttempts = null)
        {
            if (channelInstance != null)
                return channelInstance;

            var attempts = 0;
            var connectionFactory = new ConnectionFactory
            {
                HostName = ConnectionParams.HostName,
                Port = ConnectionParams.Port,
                VirtualHost = ConnectionParams.VirtualHost,
                UserName = ConnectionParams.Username,
                Password = ConnectionParams.Password,
                AutomaticRecoveryEnabled = true, // The created connection is an IRecoverable
                RequestedHeartbeat = 30
            };

            while (true)
            {
                try
                {
                    logger.Connect(ConnectionParams);

                    connection = connectionFactory.CreateConnection();
                    channelInstance = connection.CreateModel();
                    channelInstance.ConfirmSelect();

                    if (ConnectionParams.PrefetchCount > 0)
                        channelInstance.BasicQos(0, ConnectionParams.PrefetchCount, false);

                    ((IRecoverable)connection).Recovery += (sender, e) => ConnectionEventListener?.Reconnected();

                    channelInstance.ModelShutdown += (sender, eventArgs) => ConnectionEventListener?.Disconnected();
                    channelInstance.BasicReturn += (sender, eventArgs) =>
                    {
                        publishResultTaskSource?.SetResult(eventArgs.ReplyCode);
                        publishResultTaskSource = null;
                    };

                    channelInstance.BasicAcks += (sender, eventArgs) =>
                    {
                        publishResultTaskSource?.SetResult(0);
                        publishResultTaskSource = null;
                    };

                    ConnectionEventListener?.Connected();
                    logger.ConnectSuccess(ConnectionParams);

                    break;
                }
                catch (BrokerUnreachableException e)
                {
                    logger.ConnectFailed(ConnectionParams, e);

                    attempts++;
                    if (maxAttempts.HasValue && attempts > maxAttempts.Value)
                        throw;

                    await Task.Delay(ReconnectDelay);
                }
            }

            return channelInstance;
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
