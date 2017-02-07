using System;
using Tapeti.Config;

namespace Tapeti
{
    public interface IExceptionStrategy
    {
        /// <summary>
        /// Called when an exception occurs while handling a message.
        /// </summary>
        /// <param name="context">The message context if available. May be null!</param>
        /// <param name="exception">The exception instance</param>
        /// <returns>The ConsumeResponse to determine whether to requeue, dead-letter (nack) or simply ack the message.</returns>
        ConsumeResponse HandleException(IMessageContext context, Exception exception);
    }
}
