using System;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <inheritdoc />
    /// <summary>
    /// Default implementation of IExceptionStrategyContext.
    /// </summary>
    public class ExceptionStrategyContext : IExceptionStrategyContext
    {
        /// <summary>
        /// The ConsumeResult as set by the exception strategy. Defaults to Error.
        /// </summary>
        public ConsumeResult ConsumeResult { get; set; } = ConsumeResult.Error;


        /// <inheritdoc />
        public IMessageContext MessageContext { get; }

        /// <inheritdoc />
        public Exception Exception { get; }

        
        /// <inheritdoc />
        public ExceptionStrategyContext(IMessageContext messageContext, Exception exception)
        {
            MessageContext = messageContext;
            Exception = exception;
        }


        /// <inheritdoc />
        public void SetConsumeResult(ConsumeResult consumeResult)
        {
            ConsumeResult = consumeResult;
        }
    }
}
