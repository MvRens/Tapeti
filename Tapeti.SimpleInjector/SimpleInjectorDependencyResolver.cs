using System;
using System.Linq;
using SimpleInjector;

namespace Tapeti.SimpleInjector
{
    public class SimpleInjectorDependencyResolver : IDependencyInjector
    {
        private readonly Container container;

        public SimpleInjectorDependencyResolver(Container container)
        {
            this.container = container;
        }

        public T Resolve<T>() where T : class
        {
            return container.GetInstance<T>();
        }

        public object Resolve(Type type)
        {
            return container.GetInstance(type);
        }


        public void RegisterDefault<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            // ReSharper disable once SimplifyLinqExpression - not a fan of negative predicates
            if (!container.GetCurrentRegistrations().Any(ip => ip.ServiceType == typeof(TService)))
                container.Register<TService, TImplementation>();
        }


        public void RegisterPublisher(Func<IPublisher> publisher)
        {
            // ReSharper disable once SimplifyLinqExpression - still not a fan of negative predicates
            if (!container.GetCurrentRegistrations().Any(ip => ip.ServiceType == typeof(IPublisher)))
                container.Register(publisher);
        }


        public void RegisterController(Type type)
        {
            container.Register(type);
        }
    }
}
