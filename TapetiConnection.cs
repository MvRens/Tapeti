using System;
using System.Collections.Generic;
using System.Linq;
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
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string PublishExchange { get; set; } = "";
        public string SubscribeExchange { get; set; } = "";


        public IDependencyResolver DependencyResolver
        {
            get { return dependencyResolver ?? (dependencyResolver = new DefaultDependencyResolver(GetPublisher)); }
            set { dependencyResolver = value; }
        }


        private IDependencyResolver dependencyResolver;
        private List<IQueueRegistration> registrations;
        private TapetiWorker worker;
        


        public TapetiConnection WithDependencyResolver(IDependencyResolver resolver)
        {
            dependencyResolver = resolver;
            return this;
        }


        public TapetiConnection RegisterController(Type type)
        {
            var queueAttribute = type.GetCustomAttribute<QueueAttribute>();
            if (queueAttribute == null)
                throw new ArgumentException("Queue attribute required on class", nameof(type));

            if (queueAttribute.Dynamic)
            {
                if (!string.IsNullOrEmpty(queueAttribute.Name))
                    throw new ArgumentException("Dynamic queue attributes must not have a Name");

                GetRegistrations().Add(new ControllerDynamicQueueRegistration(
                    DependencyResolver.Resolve<IControllerFactory>, 
                    DependencyResolver.Resolve<IRoutingKeyStrategy>,
                    type, SubscribeExchange));
            }
            else
            {
                if (string.IsNullOrEmpty(queueAttribute.Name))
                    throw new ArgumentException("Non-dynamic queue attribute must have a Name");

                GetRegistrations().Add(new ControllerQueueRegistration(
                    DependencyResolver.Resolve<IControllerFactory>, 
                    type, SubscribeExchange, queueAttribute.Name));
            }

            (DependencyResolver as IDependencyInjector)?.RegisterController(type);
            return this;
        }


        public TapetiConnection RegisterAllControllers(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsDefined(typeof(QueueAttribute))))
                RegisterController(type);

            return this;
        }


        public TapetiConnection RegisterAllControllers()
        {
            return RegisterAllControllers(Assembly.GetCallingAssembly());
        }


        public async Task<ISubscriber> Subscribe()
        {
            if (registrations == null || registrations.Count == 0)
                throw new ArgumentException("No controllers registered");

            var subscriber = new TapetiSubscriber(GetWorker());
            await subscriber.BindQueues(registrations);

            return subscriber;
        }


        public IPublisher GetPublisher()
        {
            return new TapetiPublisher(GetWorker());
        }


        public async Task Close()
        {
            if (worker != null)
            {
                await worker.Close();
                worker = null;
            }
        }


        public void Dispose()
        {
            Close().Wait();
        }


        protected List<IQueueRegistration> GetRegistrations()
        {
            return registrations ?? (registrations = new List<IQueueRegistration>());
        }


        protected TapetiWorker GetWorker()
        {
            return worker ?? (worker = new TapetiWorker(
                DependencyResolver.Resolve<IMessageSerializer>(),
                DependencyResolver.Resolve<IRoutingKeyStrategy>())
                   {
                       HostName = HostName,
                       Port = Port,
                       VirtualHost = VirtualHost,
                       Username = Username,
                       Password = Password,
                       PublishExchange = PublishExchange
                   });
        }
    }
}
