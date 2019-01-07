using System;
using System.Linq;
using SimpleInjector;

namespace Tapeti.SimpleInjector
{
    public class SimpleInjectorDependencyResolver : IDependencyContainer
    {
        private readonly Container container;
        private readonly Lifestyle defaultsLifestyle;
        private readonly Lifestyle controllersLifestyle;

        public SimpleInjectorDependencyResolver(Container container, Lifestyle defaultsLifestyle = null, Lifestyle controllersLifestyle = null)
        {
            this.container = container;
            this.defaultsLifestyle = defaultsLifestyle;
            this.controllersLifestyle = controllersLifestyle;
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
            if (!CanRegisterDefault<TService>()) 
                return;

            if (defaultsLifestyle != null)
                container.Register<TService, TImplementation>(defaultsLifestyle);
            else
                container.Register<TService, TImplementation>();
        }

        public void RegisterDefault<TService>(Func<TService> factory) where TService : class
        {
            if (!CanRegisterDefault<TService>())
                return;

            if (defaultsLifestyle != null)
                container.Register(factory, defaultsLifestyle);
            else
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
                container.RegisterInstance(instance);
        }

        public void RegisterDefaultSingleton<TService>(Func<TService> factory) where TService : class
        {
            if (CanRegisterDefault<TService>())
                container.RegisterSingleton(factory);
        }

        public void RegisterController(Type type)
        {
            if (controllersLifestyle != null)                
                container.Register(type, type, controllersLifestyle);
            else
                container.Register(type);
        }


        private bool CanRegisterDefault<TService>() where TService : class
        {
            // ReSharper disable once SimplifyLinqExpression - not a fan of negative predicates
            return !container.GetCurrentRegistrations().Any(ip => ip.ServiceType == typeof(TService));
        }
    }
}
