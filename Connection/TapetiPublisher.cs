using System.Threading.Tasks;

namespace Tapeti.Connection
{
    public class TapetiPublisher : IPublisher
    {
        private readonly TapetiWorker worker;


        public TapetiPublisher(TapetiWorker worker)
        {
            this.worker = worker;
        }


        public Task Publish(object message)
        {
            return worker.Publish(message);
        }
    }
}
