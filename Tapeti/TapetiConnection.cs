using System;
using System.Linq;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Connection;

namespace Tapeti
{
    public class TapetiConnection : IDisposable
    {
        private readonly IConfig config;
        public TapetiConnectionParams Params { get; set; }

        private readonly Lazy<TapetiWorker> worker;


        public TapetiConnection(IConfig config)
        {
            this.config = config;
            (config.DependencyResolver as IDependencyContainer)?.RegisterDefault(GetPublisher);

            worker = new Lazy<TapetiWorker>(() => new TapetiWorker(config.DependencyResolver, config.MessageMiddleware)
            {
                ConnectionParams = Params ?? new TapetiConnectionParams()
            });
        }


        public async Task<ISubscriber> Subscribe(bool startConsuming = true)
        {
            var subscriber = new TapetiSubscriber(() => worker.Value, config.Queues.ToList());
            await subscriber.BindQueues();

            if (startConsuming)
                await subscriber.Resume();

            return subscriber;
        }


        public ISubscriber SubscribeSync()
        {
            return Subscribe().Result;
        }


        public IPublisher GetPublisher()
        {
            return new TapetiPublisher(() => worker.Value);
        }


        public async Task Close()
        {
            if (worker.IsValueCreated)
                await worker.Value.Close();
        }


        public void Dispose()
        {
            Close().Wait();
        }
    }
}
