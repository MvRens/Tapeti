using System;

namespace Tapeti
{
    public interface IRoutingKeyStrategy
    {
        string GetRoutingKey(Type messageType);
    }
}
