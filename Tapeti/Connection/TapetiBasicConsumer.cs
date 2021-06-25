using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Tapeti.Default;

namespace Tapeti.Connection
{
    /// <inheritdoc />
    /// <summary>
    /// Implements the bridge between the RabbitMQ Client consumer and a Tapeti Consumer
    /// </summary>
    internal class TapetiBasicConsumer : DefaultBasicConsumer
    {
        private readonly IConsumer consumer;
        private readonly Func<ulong, ConsumeResult, Task> onRespond;


        /// <inheritdoc />
        public TapetiBasicConsumer(IConsumer consumer, Func<ulong, ConsumeResult, Task> onRespond)
        {
            this.consumer = consumer;
            this.onRespond = onRespond;
        }


        /// <inheritdoc />
        public override void HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            ReadOnlyMemory<byte> body)
        {
            Task.Run(async () =>
            {
                try
                {
                    var response = await consumer.Consume(exchange, routingKey, new RabbitMQMessageProperties(properties), body.ToArray());
                    await onRespond(deliveryTag, response);
                }
                catch
                {
                    await onRespond(deliveryTag, ConsumeResult.Error);
                }
            });
        }
    }
}
