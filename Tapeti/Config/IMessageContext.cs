using System;
using System.Collections.Generic;
using RabbitMQ.Client;

namespace Tapeti.Config
{
    public interface IMessageContext : IDisposable
    {
        IDependencyResolver DependencyResolver { get; }

        string Queue { get; }
        string RoutingKey { get; }
        object Message { get; }
        IBasicProperties Properties { get; }

        IDictionary<string, object> Items { get; }

        /// <remarks>
        /// Controller will be null when passed to a IMessageFilterMiddleware
        /// </remarks>
        object Controller { get; }

        IBinding Binding { get; }

        IMessageContext SetupNestedContext();
    }
}
