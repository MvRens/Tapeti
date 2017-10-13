using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using Tapeti.Config;
using Tapeti.Helpers;
using Tapeti.Tasks;

namespace Tapeti.Connection
{
    public class TapetiWorker
    {
        private const int ReconnectDelay = 5000;
        private const int PublishMaxConnectAttempts = 3;

        private readonly IConfig config;
        public TapetiConnectionParams ConnectionParams { get; set; }
        public IConnectionEventListener ConnectionEventListener { get; set; }

        private readonly IMessageSerializer messageSerializer;
        private readonly IRoutingKeyStrategy routingKeyStrategy;
        private readonly IExchangeStrategy exchangeStrategy;
        private readonly Lazy<SingleThreadTaskQueue> taskQueue = new Lazy<SingleThreadTaskQueue>();
        private RabbitMQ.Client.IConnection connection;
        private IModel channelInstance;


        public TapetiWorker(IConfig config)
        {
            this.config = config;

            messageSerializer = config.DependencyResolver.Resolve<IMessageSerializer>();
            routingKeyStrategy = config.DependencyResolver.Resolve<IRoutingKeyStrategy>();
            exchangeStrategy = config.DependencyResolver.Resolve<IExchangeStrategy>();
        }


        public Task Publish(object message, IBasicProperties properties)
        {
            return Publish(message, properties, exchangeStrategy.GetExchange(message.GetType()), routingKeyStrategy.GetRoutingKey(message.GetType()));
        }


        public Task PublishDirect(object message, string queueName, IBasicProperties properties)
        {
            return Publish(message, properties, "", queueName);
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
                    var dynamicQueue = channel.QueueDeclare();
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


        private Task Publish(object message, IBasicProperties properties, string exchange, string routingKey)
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
                    (await GetChannel(PublishMaxConnectAttempts)).BasicPublish(context.Exchange, context.RoutingKey, false,
                        context.Properties, body);
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
                    connection = connectionFactory.CreateConnection();
                    channelInstance = connection.CreateModel();

                    if (ConnectionParams.PrefetchCount > 0)
                        channelInstance.BasicQos(0, ConnectionParams.PrefetchCount, false);

                    ((IRecoverable)connection).Recovery += (sender, e) => ConnectionEventListener?.Reconnected();

                    channelInstance.ModelShutdown += (sender, e) => ConnectionEventListener?.Disconnected();

                    ConnectionEventListener?.Connected();
                    break;
                }
                catch (BrokerUnreachableException)
                {
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
