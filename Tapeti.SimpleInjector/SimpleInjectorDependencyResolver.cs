using System.Linq;
using System.Reflection;
using SimpleInjector;
using Tapeti.Annotations;
using Tapeti.Default;
using System.Collections.Generic;

namespace Tapeti.SimpleInjector
{
    public class SimpleInjectorDependencyResolver : IDependencyResolver
    {
        private readonly Container container;

        public SimpleInjectorDependencyResolver(Container container, bool registerDefaults = true)
        {
            this.container = container;

            if (registerDefaults)
                RegisterDefaults();
        }

        public T Resolve<T>() where T : class
        {
            return container.GetInstance<T>();
        }


        public SimpleInjectorDependencyResolver RegisterDefaults()
        {
            var currentRegistrations = container.GetCurrentRegistrations();

            IfUnregistered<IControllerFactory, SimpleInjectorControllerFactory>(currentRegistrations);
            IfUnregistered<IMessageSerializer, DefaultMessageSerializer>(currentRegistrations);
            IfUnregistered<IRoutingKeyStrategy, DefaultRoutingKeyStrategy>(currentRegistrations);

            return this;
        }


        public SimpleInjectorDependencyResolver RegisterAllControllers(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsDefined(typeof(QueueAttribute))))
                container.Register(type);

            return this;
        }


        private void IfUnregistered<TService, TImplementation>(IEnumerable<InstanceProducer> currentRegistrations) where TService : class where TImplementation: class, TService
        {
            // ReSharper disable once SimplifyLinqExpression - not a fan of negative predicates
            if (!currentRegistrations.Any(ip => ip.ServiceType == typeof(TService)))
                container.Register<TService, TImplementation>();
        }
    }
}
