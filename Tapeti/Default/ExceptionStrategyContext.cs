using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            get
            {
                if (handlingResult == null)
                {
                    handlingResult = new HandlingResultBuilder();
                }
                return handlingResult;
            }

            set
            {
                handlingResult = value;
            }
        }
    }
}
