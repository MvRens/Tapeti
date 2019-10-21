using System;
using Unity;
using Unity.Lifetime;

namespace Tapeti.UnityContainer
{
    /// <inheritdoc />
    /// <summary>
    /// Dependency resolver and container implementation for SimpleInjector.
    /// </summary>
    public class UnityDependencyResolver : IDependencyContainer
    {
        private readonly IUnityContainer container;


        /// <inheritdoc />
        public UnityDependencyResolver(IUnityContainer container)
        {
            this.container = container;
        }


        /// <inheritdoc />
        public T Resolve<T>() where T : class
        {
            return container.Resolve<T>();
        }

        /// <inheritdoc />
        public object Resolve(Type type)
        {
            return container.Resolve(type);
        }


        /// <inheritdoc />
        public void RegisterDefault<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            if (container.IsRegistered(typeof(TService)))
                return;

            container.RegisterType<TService, TImplementation>();
        }

        /// <inheritdoc />
        public void RegisterDefault<TService>(Func<TService> factory) where TService : class
        {
            if (container.IsRegistered(typeof(TService)))
                return;

            container.RegisterFactory<TService>(c => factory());
        }


        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            if (container.IsRegistered(typeof(TService)))
                return;

            container.RegisterSingleton<TService, TImplementation>();
        }

        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService>(TService instance) where TService : class
        {
            if (container.IsRegistered(typeof(TService)))
                return;

            container.RegisterInstance(instance);
        }

        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService>(Func<TService> factory) where TService : class
        {
            if (container.IsRegistered(typeof(TService)))
                return;

            container.RegisterFactory<TService>(c => factory(), new SingletonLifetimeManager());
        }


        /// <inheritdoc />
        public void RegisterController(Type type)
        {
            container.RegisterType(type);
        }
    }
}
