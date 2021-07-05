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
            // RabbitMQ.Client 6+ re-uses the body memory. Unfortunately Newtonsoft.Json does not support deserializing
            // from Span/ReadOnlyMemory yet so we still need to use ToArray and allocate heap memory for it. When support
            // is implemented we need to rethink the way the body is passed around and maybe deserialize it sooner
            // (which changes exception handling, which is now done in TapetiConsumer exclusively).
            //
            // See also: https://github.com/JamesNK/Newtonsoft.Json/issues/1761
            var bodyArray = body.ToArray();
            
            Task.Run(async () =>
            {
                try
                {
                    var response = await consumer.Consume(exchange, routingKey, new RabbitMQMessageProperties(properties), bodyArray);
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
