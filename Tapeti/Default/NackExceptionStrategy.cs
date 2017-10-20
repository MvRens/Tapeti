using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Default
{
    public class NackExceptionStrategy : IExceptionStrategy
    {
        public void HandleException(IExceptionStrategyContext context)
        {
            context.HandlingResult.ConsumeResponse = ConsumeResponse.Nack;
        }
    }
}
