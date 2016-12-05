using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tapeti.Connection
{
    public class TapetiSubscriber : ISubscriber
    {
        private readonly Func<TapetiWorker> workerFactory;


        public TapetiSubscriber(Func<TapetiWorker> workerFactory)
        {
            this.workerFactory = workerFactory;
        }


        public async Task BindQueues(IEnumerable<IQueueRegistration> registrations)
        {
            await Task.WhenAll(registrations.Select(registration => workerFactory().Subscribe(registration)).ToList());
        }
    }
}
