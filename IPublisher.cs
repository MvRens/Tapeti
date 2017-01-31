using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Tapeti
{
    public interface IPublisher
    {
        Task Publish(object message);
    }


    public interface IAdvancedPublisher : IPublisher
    {
        Task Publish(object message, IBasicProperties properties);
        Task PublishDirect(object message, string queueName, IBasicProperties properties);
    }
}
