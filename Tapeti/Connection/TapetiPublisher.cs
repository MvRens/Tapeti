namespace Tapeti.Connection
{
    public class TapetiPublisher : IPublisher
    {
        private readonly TapetiWorker worker;


        public TapetiPublisher(TapetiWorker worker)
        {
            this.worker = worker;
        }
    }
}
