using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tapeti.Connection
{
    public class TapetiSubscriber : ISubscriber
    {
        private readonly TapetiWorker worker;


        public TapetiSubscriber(TapetiWorker worker)
        {
            this.worker = worker;
        }


        public async Task BindQueues(IEnumerable<IQueueRegistration> registrations)
        {
            await Task.WhenAll(registrations.Select(registration => worker.Subscribe(registration)).ToList());
        }
    }
}
