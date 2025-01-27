using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Tapeti.Default;

namespace Tapeti.Connection
{
    /// <summary>
    /// Called to report the result of a consumed message back to RabbitMQ.
    /// </summary>
    /// <param name="expectedConnectionReference">The connection reference on which the consumed message was received</param>
    /// <param name="deliveryTag">The delivery tag of the consumed message</param>
    /// <param name="result">The result which should be sent back</param>
    public delegate Task ResponseFunc(long expectedConnectionReference, ulong deliveryTag, ConsumeResult result);


    /// <summary>
    /// Implements the bridge between the RabbitMQ Client consumer and a Tapeti Consumer
    /// </summary>
    internal class TapetiBasicConsumer : AsyncDefaultBasicConsumer
    {
        private readonly IConsumer consumer;
        private readonly IMessageHandlerTracker messageHandlerTracker;
        private readonly long connectionReference;
        private readonly ResponseFunc onRespond;


        /// <inheritdoc />
        public TapetiBasicConsumer(IConsumer consumer, IMessageHandlerTracker messageHandlerTracker, long connectionReference, ResponseFunc onRespond)
        {
            this.consumer = consumer;
            this.messageHandlerTracker = messageHandlerTracker;
            this.connectionReference = connectionReference;
            this.onRespond = onRespond;
        }


        /// <inheritdoc />
        public override async Task HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            ReadOnlyMemory<byte> body)
        {
            messageHandlerTracker.Enter();
            try
            {
                // RabbitMQ.Client 6+ re-uses the body memory. Unfortunately Newtonsoft.Json does not support deserializing
                // from Span/ReadOnlyMemory yet so we still need to use ToArray and allocate heap memory for it. When support
                // is implemented we need to rethink the way the body is passed around and maybe deserialize it sooner
                // (which changes exception handling, which is now done in TapetiConsumer exclusively).
                //
                // See also: https://github.com/JamesNK/Newtonsoft.Json/issues/1761
                var bodyArray = body.ToArray();

                try
                {
                    var response = await consumer.Consume(exchange, routingKey, new RabbitMQMessageProperties(properties), bodyArray).ConfigureAwait(false);
                    await onRespond(connectionReference, deliveryTag, response).ConfigureAwait(false);
                }
                catch
                {
                    await onRespond(connectionReference, deliveryTag, ConsumeResult.Error).ConfigureAwait(false);
                }
            }
            finally
            {
                messageHandlerTracker.Exit();
            }
        }
    }
}
