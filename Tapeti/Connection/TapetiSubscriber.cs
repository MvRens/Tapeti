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


        public TapetiSubscriber(Func<TapetiWorker> workerFactory)
        {
            this.workerFactory = workerFactory;
        }


        public async Task BindQueues(IEnumerable<IQueue> queues)
        {
            await Task.WhenAll(queues.Select(queue => workerFactory().Subscribe(queue)).ToList());
        }
    }
}
