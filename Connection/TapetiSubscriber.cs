using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tapeti.Connection
{
    public class TapetiSubscriber : ISubscriber
    {
        private readonly TapetiWorker worker;


        public TapetiSubscriber(TapetiWorker worker, IEnumerable<IMessageHandlerRegistration> registrations)
        {
            this.worker = worker;

            ApplyTopology(registrations);
        }


        private void ApplyTopology(IEnumerable<IMessageHandlerRegistration> registrations)
        {
            foreach (var registration in registrations)
                worker.ApplyTopology(registration);
        }
    }
}
