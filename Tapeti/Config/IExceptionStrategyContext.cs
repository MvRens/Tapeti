using System;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Config
{
    public interface IExceptionStrategyContext
    {
        IMessageContext MessageContext { get; }

        Exception Exception { get; }

        HandlingResultBuilder HandlingResult { get; set; }
    }
}
