using System.Collections.Generic;
using RabbitMQ.Client;

namespace Tapeti.Config
{
    public interface IMessageContext
    {
        IDependencyResolver DependencyResolver { get; }

        object Controller { get; }
        object Message { get; }
        IBasicProperties Properties { get; }

        IDictionary<string, object> Items { get; }
    }
}
