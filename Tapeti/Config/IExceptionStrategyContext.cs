using System;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Config
{
    /// <summary>
    /// Provides access to information about the message being consumed.
    /// Allows the strategy to determine how the exception should be handled.
    /// </summary>
    public interface IExceptionStrategyContext
    {
        /// <summary>
        /// Provides access to the message context.
        /// </summary>
        IMessageContext MessageContext { get; }

        /// <summary>
        /// Contains the exception being handled.
        /// </summary>
        Exception Exception { get; }

        /// <summary>
        /// Determines how the message has been handled. Defaults to Error.
        /// </summary>
        /// <param name="consumeResult"></param>
        void SetConsumeResult(ConsumeResult consumeResult);
    }
}
