using System;
using System.Linq;
using SimpleInjector;

namespace Tapeti.SimpleInjector
{
    /// <inheritdoc />
    /// <summary>
    /// Dependency resolver and container implementation for SimpleInjector.
    /// </summary>
    public class SimpleInjectorDependencyResolver : IDependencyContainer
    {
        private readonly Container container;
        private readonly Lifestyle defaultsLifestyle;
        private readonly Lifestyle controllersLifestyle;

        /// <inheritdoc />
        public SimpleInjectorDependencyResolver(Container container, Lifestyle defaultsLifestyle = null, Lifestyle controllersLifestyle = null)
        {
            this.container = container;
            this.defaultsLifestyle = defaultsLifestyle;
            this.controllersLifestyle = controllersLifestyle;
        }


        /// <inheritdoc />
        public T Resolve<T>() where T : class
        {
            return container.GetInstance<T>();
        }

        /// <inheritdoc />
        public object Resolve(Type type)
        {
            return container.GetInstance(type);
        }


        /// <inheritdoc />
        public void RegisterDefault<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            if (!CanRegisterDefault<TService>()) 
                return;

            if (defaultsLifestyle != null)
                container.Register<TService, TImplementation>(defaultsLifestyle);
            else
                container.Register<TService, TImplementation>();
        }

        /// <inheritdoc />
        public void RegisterDefault<TService>(Func<TService> factory) where TService : class
        {
            if (!CanRegisterDefault<TService>())
                return;

            if (defaultsLifestyle != null)
                container.Register(factory, defaultsLifestyle);
            else
                container.Register(factory);
        }


        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            if (CanRegisterDefault<TService>())
                container.RegisterSingleton<TService, TImplementation>();
        }

        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService>(TService instance) where TService : class
        {
            if (CanRegisterDefault<TService>())
                container.RegisterInstance(instance);
        }

        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService>(Func<TService> factory) where TService : class
        {
            if (CanRegisterDefault<TService>())
                container.RegisterSingleton(factory);
        }

        /// <inheritdoc />
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
