using System;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Tapeti.Connection
{
    public class TapetiPublisher : IInternalPublisher
    {
        private readonly Func<TapetiWorker> workerFactory;


        public TapetiPublisher(Func<TapetiWorker> workerFactory)
        {
            this.workerFactory = workerFactory;
        }


        public Task Publish(object message)
        {
            return workerFactory().Publish(message, null);
        }


        public Task Publish(object message, IBasicProperties properties)
        {
            return workerFactory().Publish(message, properties);
        }


        public Task PublishDirect(object message, string queueName, IBasicProperties properties)
        {
            return workerFactory().PublishDirect(message, queueName, properties);
        }
    }
}
