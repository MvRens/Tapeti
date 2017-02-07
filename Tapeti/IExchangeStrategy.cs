using System;

namespace Tapeti
{
    public interface IExchangeStrategy
    {
        string GetExchange(Type messageType);
    }
}
