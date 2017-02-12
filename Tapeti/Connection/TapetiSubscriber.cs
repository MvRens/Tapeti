using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Connection
{
    public class TapetiSubscriber : ISubscriber
    {
        private readonly Func<TapetiWorker> workerFactory;
        private readonly List<IQueue> queues;
        private bool consuming;


        public TapetiSubscriber(Func<TapetiWorker> workerFactory, IEnumerable<IQueue> queues)
        {
            this.workerFactory = workerFactory;
            this.queues = queues.ToList();
        }


        public Task BindQueues()
        {           
            return Task.WhenAll(queues.Select(queue => workerFactory().Subscribe(queue)).ToList());
        }


        public Task Resume()
        {
            if (consuming)
                return Task.CompletedTask;

            consuming = true;
            return Task.WhenAll(queues.Select(queue => workerFactory().Consume(queue.Name, queue.Bindings)).ToList());
        }
    }
}
