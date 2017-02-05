using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using Tapeti.Config;
using Tapeti.Tasks;

namespace Tapeti.Connection
{
    public class TapetiWorker
    {
        public TapetiConnectionParams ConnectionParams { get; set; }
        public string SubscribeExchange { get; set; }

        private readonly IDependencyResolver dependencyResolver;
        private readonly IReadOnlyList<IMessageMiddleware> messageMiddleware;
        private readonly IMessageSerializer messageSerializer;
        private readonly IRoutingKeyStrategy routingKeyStrategy;
        private readonly Lazy<SingleThreadTaskQueue> taskQueue = new Lazy<SingleThreadTaskQueue>();
        private RabbitMQ.Client.IConnection connection;
        private IModel channelInstance;


        public TapetiWorker(IDependencyResolver dependencyResolver, IReadOnlyList<IMessageMiddleware> messageMiddleware)
        {
            this.dependencyResolver = dependencyResolver;
            this.messageMiddleware = messageMiddleware;
            messageSerializer = dependencyResolver.Resolve<IMessageSerializer>();
            routingKeyStrategy = dependencyResolver.Resolve<IRoutingKeyStrategy>();
        }


        public Task Publish(object message, IBasicProperties properties)
        {
            // TODO use exchange strategy!
            return Publish(message, properties, SubscribeExchange, routingKeyStrategy.GetRoutingKey(message.GetType()));
        }


        public Task PublishDirect(object message, string queueName, IBasicProperties properties)
        {
            return Publish(message, properties, "", queueName);
        }


        public Task Consume(string queueName, IEnumerable<IBinding> bindings)
        {
            return taskQueue.Value.Add(async () =>
            {
                (await GetChannel()).BasicConsume(queueName, false, new TapetiConsumer(this, queueName, dependencyResolver, bindings, messageMiddleware));
            }).Unwrap();
        }


        public async Task Subscribe(IQueue queue)
        {
            var queueName = await taskQueue.Value.Add(async () =>
            {
                var channel = await GetChannel();

                if (queue.Dynamic)
                {
                    var dynamicQueue = channel.QueueDeclare();

                    foreach (var binding in queue.Bindings)
                    {
                        var routingKey = routingKeyStrategy.GetRoutingKey(binding.MessageClass);
                        channel.QueueBind(dynamicQueue.QueueName, SubscribeExchange, routingKey);

                        (binding as IDynamicQueueBinding)?.SetQueueName(dynamicQueue.QueueName);
                    }
                    
                    return dynamicQueue.QueueName;
                }

                channel.QueueDeclarePassive(queue.Name);
                return queue.Name;
            }).Unwrap();

            await Consume(queueName, queue.Bindings);
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
            return taskQueue.Value.Add(async () =>
            {
                var messageProperties = properties ?? new BasicProperties();
                if (messageProperties.Timestamp.UnixTime == 0)
                    messageProperties.Timestamp = new AmqpTimestamp(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());

                var body = messageSerializer.Serialize(message, messageProperties);

                (await GetChannel())
                    .BasicPublish(exchange, routingKey, false, messageProperties, body);
            }).Unwrap();

        }

        /// <remarks>
        /// Only call this from a task in the taskQueue to ensure IModel is only used 
        /// by a single thread, as is recommended in the RabbitMQ .NET Client documentation.
        /// </remarks>
        private async Task<IModel> GetChannel()
        {
            if (channelInstance != null)
                return channelInstance;

            var connectionFactory = new ConnectionFactory
            {
                HostName = ConnectionParams.HostName,
                Port = ConnectionParams.Port,
                VirtualHost = ConnectionParams.VirtualHost,
                UserName = ConnectionParams.Username,
                Password = ConnectionParams.Password,
                AutomaticRecoveryEnabled = true,
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

                    break;
                }
                catch (BrokerUnreachableException)
                {
                    await Task.Delay(5000);
                }
            }

            return channelInstance;
        }
    }
}
