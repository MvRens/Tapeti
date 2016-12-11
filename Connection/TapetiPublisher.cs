using System;
using System.Threading.Tasks;

namespace Tapeti.Connection
{
    public class TapetiPublisher : IPublisher
    {
        private readonly Func<TapetiWorker> workerFactory;


        public TapetiPublisher(Func<TapetiWorker> workerFactory)
        {
            this.workerFactory = workerFactory;
        }


        public Task Publish(object message)
        {
            return workerFactory().Publish(message);
        }
    }
}
