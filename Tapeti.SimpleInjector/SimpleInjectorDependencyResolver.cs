using System;
using System.Linq;
using SimpleInjector;

namespace Tapeti.SimpleInjector
{
    public class SimpleInjectorDependencyResolver : IDependencyContainer
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
            if (CanRegisterDefault<TService>())
                container.Register<TService, TImplementation>();
        }

        public void RegisterDefault<TService>(Func<TService> factory) where TService : class
        {
            if (CanRegisterDefault<TService>())
                container.Register(factory);
        }

        public void RegisterDefaultSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            if (CanRegisterDefault<TService>())
                container.RegisterSingleton<TService, TImplementation>();
        }

        public void RegisterDefaultSingleton<TService>(TService instance) where TService : class
        {
            if (CanRegisterDefault<TService>())
                container.RegisterSingleton(instance);
        }

        public void RegisterDefaultSingleton<TService>(Func<TService> factory) where TService : class
        {
            if (CanRegisterDefault<TService>())
                container.RegisterSingleton(factory);
        }

        public void RegisterController(Type type)
        {
            container.Register(type);
        }


        private bool CanRegisterDefault<TService>() where TService : class
        {
            // ReSharper disable once SimplifyLinqExpression - not a fan of negative predicates
            return !container.GetCurrentRegistrations().Any(ip => ip.ServiceType == typeof(TService));
        }
    }
}
