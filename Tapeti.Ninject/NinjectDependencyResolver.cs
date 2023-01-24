using System;
using System.Linq;
using Ninject;

namespace Tapeti.Ninject
{
    /// <summary>
    /// Dependency resolver and container implementation for Ninject.
    /// </summary>
    public class NinjectDependencyResolver : IDependencyContainer
    {
        private readonly IKernel kernel;


        /// <summary>
        /// </summary>
        public NinjectDependencyResolver(IKernel kernel)
        {
            this.kernel = kernel;
        }


        /// <inheritdoc />
        public T Resolve<T>() where T : class
        {
            return kernel.Get<T>();
        }

        /// <inheritdoc />
        public object Resolve(Type type)
        {
            return kernel.Get(type);
        }


        /// <inheritdoc />
        public void RegisterDefault<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            if (kernel.GetBindings(typeof(TService)).Any())
                return;

            kernel.Bind<TService>().To<TImplementation>();
        }

        /// <inheritdoc />
        public void RegisterDefault<TService>(Func<TService> factory) where TService : class
        {
            if (kernel.GetBindings(typeof(TService)).Any())
                return;

            kernel.Bind<TService>().ToMethod(_ => factory());
        }


        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            if (kernel.GetBindings(typeof(TService)).Any())
                return;

            kernel.Bind<TService>().To<TImplementation>().InSingletonScope();
        }

        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService>(TService instance) where TService : class
        {
            if (kernel.GetBindings(typeof(TService)).Any())
                return;

            kernel.Bind<TService>().ToConstant(instance);
        }

        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService>(Func<TService> factory) where TService : class
        {
            if (kernel.GetBindings(typeof(TService)).Any())
                return;

            kernel.Bind<TService>().ToMethod(_ => factory()).InSingletonScope();
        }


        /// <inheritdoc />
        public void RegisterController(Type type)
        {
            kernel.Bind(type).ToSelf();
        }
    }
}
