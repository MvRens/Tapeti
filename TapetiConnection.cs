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

        public IControllerFactory ControllerFactory
        {
            get { return controllerFactory ?? (controllerFactory = new DefaultControllerFactory()); }
            set { controllerFactory = value; }
        }


        public IRoutingKeyStrategy RoutingKeyStrategy
        {
            get { return routingKeyStrategy ?? (routingKeyStrategy = new DefaultRoutingKeyStrategy()); }
            set { routingKeyStrategy = value; }
        }


        private IControllerFactory controllerFactory;
        private IRoutingKeyStrategy routingKeyStrategy;
        private List<IMessageHandlerRegistration> registrations;
        private TapetiWorker worker;
        


        public TapetiConnection WithControllerFactory(IControllerFactory factory)
        {
            controllerFactory = factory;
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

                GetRegistrations().Add(new ControllerDynamicQueueRegistration(controllerFactory, routingKeyStrategy, type));
            }
            else
            {
                if (string.IsNullOrEmpty(queueAttribute.Name))
                    throw new ArgumentException("Non-dynamic queue attribute must have a Name");

                GetRegistrations().Add(new ControllerQueueRegistration(controllerFactory, routingKeyStrategy, type, queueAttribute.Name));
            }

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


        public ISubscriber Subscribe()
        {
            if (registrations == null || registrations.Count == 0)
                throw new ArgumentException("No controllers registered");

            return new TapetiSubscriber(GetWorker(), registrations);
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


        protected List<IMessageHandlerRegistration> GetRegistrations()
        {
            return registrations ?? (registrations = new List<IMessageHandlerRegistration>());
        }


        protected TapetiWorker GetWorker()
        {
            return worker ?? (worker = new TapetiWorker
                   {
                       HostName = HostName,
                       Port = Port,
                       VirtualHost = VirtualHost,
                       Username = Username,
                       Password = Password
                   });
        }
    }
}
