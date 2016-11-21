using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using Tapeti.Tasks;

namespace Tapeti.Connection
{
    public class TapetiWorker
    {
        public TapetiConnectionParams ConnectionParams { get; set; }
        public string PublishExchange { get; set; }


        private readonly IMessageSerializer messageSerializer;
        private readonly IRoutingKeyStrategy routingKeyStrategy;
        private readonly Lazy<SingleThreadTaskQueue> taskQueue = new Lazy<SingleThreadTaskQueue>();
        private IConnection connection;
        private IModel channel;


        public TapetiWorker(IMessageSerializer messageSerializer, IRoutingKeyStrategy routingKeyStrategy)
        {
            this.messageSerializer = messageSerializer;
            this.routingKeyStrategy = routingKeyStrategy;
        }


        public Task Publish(object message)
        {
            return taskQueue.Value.Add(async () =>
            {
                var properties = new BasicProperties();
                var body = messageSerializer.Serialize(message, properties);

                (await GetChannel())
                    .BasicPublish(PublishExchange, routingKeyStrategy.GetRoutingKey(message.GetType()), false,
                        properties, body);
            }).Unwrap();
        }


        public Task Subscribe(string queueName, IQueueRegistration queueRegistration)
        {
            return taskQueue.Value.Add(async () =>
            {
                (await GetChannel())
                    .BasicConsume(queueName, false, new TapetiConsumer(this, messageSerializer, queueRegistration));
            }).Unwrap();
        }


        public async Task Subscribe(IQueueRegistration registration)
        {
            var queueName = await taskQueue.Value.Add(async () => 
                registration.BindQueue(await GetChannel()))
                .Unwrap();

            await Subscribe(queueName, registration);
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
                if (channel != null)
                {
                    channel.Dispose();
                    channel = null;
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


        /// <remarks>
        /// Only call this from a task in the taskQueue to ensure IModel is only used 
        /// by a single thread, as is recommended in the RabbitMQ .NET Client documentation.
        /// </remarks>
        private async Task<IModel> GetChannel()
        {
            if (channel != null)
                return channel;

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
                    channel = connection.CreateModel();

                    break;
                }
                catch (BrokerUnreachableException)
                {
                    await Task.Delay(5000);
                }
            }

            return channel;
        }
    }
}
