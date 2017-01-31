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

        /// <summary>
        /// Controller will be null when passed to an IBindingFilter
        /// </summary>
        object Controller { get; }
    }
}
