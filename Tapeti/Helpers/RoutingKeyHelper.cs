using System;
using System.Reflection;
using Tapeti.Annotations;

namespace Tapeti.Helpers
{
    /// <summary>
    /// Helper class for compositing a routing key with support for the RoutingKey attribute.
    /// Should be used by all implementations of IRoutingKeyStrategy unless there is a good reason not to.
    /// </summary>
    public static class RoutingKeyHelper
    {
        /// <summary>
        /// Applies the RoutingKey attribute for the specified messageClass.
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="applyStrategy">Called when the strategy needs to be applied to the message class to generate a routing key.
        /// Will not be called if the Full property is specified on the RoutingKey attribute.</param>
        public static string Decorate(Type messageType, Func<string> applyStrategy)
        {
            var routingKeyAttribute = messageType.GetCustomAttribute<RoutingKeyAttribute>();
            if (routingKeyAttribute == null)
                return applyStrategy();

            if (!string.IsNullOrEmpty(routingKeyAttribute.Full))
                return routingKeyAttribute.Full;

            return (routingKeyAttribute.Prefix ?? "") + applyStrategy() + (routingKeyAttribute.Postfix ?? "");
        }
    }
}
