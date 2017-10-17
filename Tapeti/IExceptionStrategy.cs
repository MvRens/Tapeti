using System;
using Tapeti.Config;

namespace Tapeti
{
    public interface IExceptionStrategy
    {
        /// <summary>
        /// Called when an exception occurs while handling a message.
        /// </summary>
        /// <param name="context">The exception strategy context containing the necessary data including the message context and the thrown exception.
        /// Also the response to the message can be set.
        /// If there is any other handling of the message than the expected default than HandlingResult.MessageFutureAction must be set accordingly. </param>
        void HandleException(IExceptionStrategyContext context);
    }
}
