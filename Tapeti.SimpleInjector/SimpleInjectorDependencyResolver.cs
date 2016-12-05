using System;
using System.Linq;
using System.Reflection;
using SimpleInjector;
using Tapeti.Annotations;
using Tapeti.Default;
using System.Collections.Generic;

namespace Tapeti.SimpleInjector
{
    public class SimpleInjectorDependencyResolver : IDependencyResolver, IDependencyInjector
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


        public void RegisterPublisher(IPublisher publisher)
        {
            IfUnregistered<IPublisher>(container.GetCurrentRegistrations(), () => container.RegisterSingleton(publisher));
        }


        public void RegisterController(Type type)
        {
            container.Register(type);
        }


        public SimpleInjectorDependencyResolver RegisterDefaults()
        {
            var currentRegistrations = container.GetCurrentRegistrations();

            IfUnregistered<IControllerFactory, SimpleInjectorControllerFactory>(currentRegistrations);
            IfUnregistered<IMessageSerializer, DefaultMessageSerializer>(currentRegistrations);
            IfUnregistered<IRoutingKeyStrategy, DefaultRoutingKeyStrategy>(currentRegistrations);

            return this;
        }


        private void IfUnregistered<TService, TImplementation>(IEnumerable<InstanceProducer> currentRegistrations) where TService : class where TImplementation: class, TService
        {
            // ReSharper disable once SimplifyLinqExpression - not a fan of negative predicates
            if (!currentRegistrations.Any(ip => ip.ServiceType == typeof(TService)))
                container.Register<TService, TImplementation>();
        }

        private void IfUnregistered<TService>(IEnumerable<InstanceProducer> currentRegistrations, Action register) where TService : class
        {
            // ReSharper disable once SimplifyLinqExpression - not a fan of negative predicates
            if (!currentRegistrations.Any(ip => ip.ServiceType == typeof(TService)))
                register();
        }
    }
}
