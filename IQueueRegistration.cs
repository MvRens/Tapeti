using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Tapeti
{
    public interface IQueueRegistration
    {
        string BindQueue(IModel channel);

        bool Accept(object message);
        Task Visit(object message);
    }
}
