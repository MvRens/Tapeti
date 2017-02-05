using System;
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
                ConnectionParams = Params ?? new TapetiConnectionParams(),
                SubscribeExchange = config.SubscribeExchange
            });
        }


        public async Task<ISubscriber> Subscribe()
        {
            var subscriber = new TapetiSubscriber(() => worker.Value);
            await subscriber.BindQueues(config.Queues);

            return subscriber;
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
