using System;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti
{
    /// <summary>
    /// Processes incoming messages.
    /// </summary>
    public interface IConsumer
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="exchange">The exchange from which the message originated</param>
        /// <param name="routingKey">The routing key the message was sent with</param>
        /// <param name="properties">Metadata included in the message</param>
        /// <param name="body">The raw body of the message</param>
        /// <returns></returns>
        Task<ConsumeResult> Consume(string exchange, string routingKey, IMessageProperties properties, ReadOnlyMemory<byte> body);
    }
}
