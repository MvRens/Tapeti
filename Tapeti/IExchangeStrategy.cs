using System;

namespace Tapeti
{
    /// <summary>
    /// Translates message classes into their target exchange.
    /// </summary>
    public interface IExchangeStrategy
    {
        /// <summary>
        /// Determines the exchange belonging to the given message class.
        /// </summary>
        /// <param name="messageType"></param>
        /// <returns></returns>
        string GetExchange(Type messageType);
    }
}
