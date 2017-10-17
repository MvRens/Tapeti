using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    public interface IExceptionStrategyContext
    {
        IMessageContext MessageContext { get; }

        Exception Exception { get; }

        HandlingResultBuilder HandlingResult { get; set; }
    }
}
