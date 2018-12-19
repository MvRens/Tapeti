using System;
using Tapeti.Config;

namespace Tapeti.Default
{
    public class ExceptionStrategyContext : IExceptionStrategyContext
    {
        internal ExceptionStrategyContext(IMessageContext messageContext, Exception exception)
        {
            MessageContext = messageContext;
            Exception = exception;
        }

        public IMessageContext MessageContext { get; }

        public Exception Exception { get; }

        private HandlingResultBuilder handlingResult;
        public HandlingResultBuilder HandlingResult
        {
            get => handlingResult ?? (handlingResult = new HandlingResultBuilder());
            set => handlingResult = value;
        }
    }
}
