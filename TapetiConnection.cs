using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Annotations;
using Tapeti.Connection;
using Tapeti.Default;
using Tapeti.Registration;

namespace Tapeti
{
    public class TapetiConnection : IDisposable
    {
        public TapetiConnectionParams Params { get; set; }

        public string PublishExchange { get; set; } = "";
        public string SubscribeExchange { get; set; } = "";


        public IDependencyResolver DependencyResolver
        {
            get
            {
                if (dependencyResolver == null)
                    DependencyResolver = new DefaultDependencyResolver();

                return dependencyResolver;
            }
            set
            {
                dependencyResolver = value;

                var dependencyInjector = value as IDependencyInjector;
                dependencyInjector?.RegisterPublisher(GetPublisher());
            }
        }


        private IDependencyResolver dependencyResolver;
        private readonly Lazy<List<IQueueRegistration>> registrations = new Lazy<List<IQueueRegistration>>();
        private readonly Lazy<TapetiWorker> worker;


        public TapetiConnection()
        {
            worker = new Lazy<TapetiWorker>(() => new TapetiWorker(
                DependencyResolver.Resolve<IMessageSerializer>(),
                DependencyResolver.Resolve<IRoutingKeyStrategy>())
            {
                ConnectionParams = Params ?? new TapetiConnectionParams(),
                PublishExchange = PublishExchange
            });
        }


        public TapetiConnection WithDependencyResolver(IDependencyResolver resolver)
        {
            DependencyResolver = resolver;
            return this;
        }


        public async Task<ISubscriber> Subscribe()
        {
            if (!registrations.IsValueCreated || registrations.Value.Count == 0)
                throw new ArgumentException("No controllers registered");

            var subscriber = new TapetiSubscriber(() => worker.Value);
            await subscriber.BindQueues(registrations.Value);

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
