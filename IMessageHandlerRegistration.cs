using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Tapeti
{
    public interface IMessageHandlerRegistration
    {
        void ApplyTopology(IModel channel);

        bool Accept(object message);
        Task Visit(object message);
    }
}
