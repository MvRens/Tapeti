using System.Threading.Tasks;
using RabbitMQ.Client.Framing;

namespace Tapeti.Saga
{
    public static class SagaExtensions
    {
        public static Task Publish<T>(this IPublisher publisher, object message, ISaga<T> saga) where T : class
        {
            return ((IAdvancedPublisher)publisher).Publish(message, new BasicProperties
            {
                CorrelationId = saga.Id
            });
        }
    }
}
