using RabbitMQ.Client;

namespace Tapeti.Config
{
    public interface IPublishContext
    {
        IDependencyResolver DependencyResolver { get; }

        string Exchange { get; }
        string RoutingKey { get; }
        object Message { get; }
        IBasicProperties Properties { get; }
    }
}
