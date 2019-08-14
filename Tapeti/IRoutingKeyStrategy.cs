using System;

namespace Tapeti
{
    /// <summary>
    /// Translates message classes into routing keys.
    /// </summary>
    public interface IRoutingKeyStrategy
    {
        /// <summary>
        /// Determines the routing key for the given message class.
        /// </summary>
        /// <param name="messageType"></param>
        /// <returns></returns>
        string GetRoutingKey(Type messageType);
    }
}
