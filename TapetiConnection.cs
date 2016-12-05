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


        public TapetiConnection RegisterController(Type type)
        {
            var queueAttribute = type.GetCustomAttribute<MessageController>();
            if (queueAttribute == null)
                throw new ArgumentException("Queue attribute required on class", nameof(type));

            if (queueAttribute.Dynamic)
            {
                if (!string.IsNullOrEmpty(queueAttribute.Name))
                    throw new ArgumentException("Dynamic queue attributes must not have a Name");

                registrations.Value.Add(new ControllerDynamicQueueRegistration(
                    DependencyResolver.Resolve<IControllerFactory>,
                    DependencyResolver.Resolve<IRoutingKeyStrategy>,
                    type, SubscribeExchange));
            }
            else
            {
                if (string.IsNullOrEmpty(queueAttribute.Name))
                    throw new ArgumentException("Non-dynamic queue attribute must have a Name");

                registrations.Value.Add(new ControllerQueueRegistration(
                    DependencyResolver.Resolve<IControllerFactory>,
                    type, SubscribeExchange, queueAttribute.Name));
            }

            (DependencyResolver as IDependencyInjector)?.RegisterController(type);
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
