using System.Collections.Generic;

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
                registration.ApplyTopology(worker.GetChannel());
        }
    }
}
