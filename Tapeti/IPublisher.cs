using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Tapeti
{
    // Note: Tapeti assumes every implementation of IPublisher can also be cast to an IInternalPublisher.
    // The distinction is made on purpose to trigger code-smells in non-Tapeti code when casting.
    public interface IPublisher
    {
        Task Publish(object message);
    }


    public interface IInternalPublisher : IPublisher
    {
        Task Publish(object message, IBasicProperties properties);
        Task PublishDirect(object message, string queueName, IBasicProperties properties);
    }
}
