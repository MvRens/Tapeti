using System;
using Castle.MicroKernel.Registration;
using Castle.Windsor;

namespace Tapeti.CastleWindsor
{
    /// <inheritdoc />
    /// <summary>
    /// Dependency resolver and container implementation for Castle Windsor.
    /// </summary>
    public class WindsorDependencyResolver : IDependencyContainer
    {
        private readonly IWindsorContainer container;


        /// <inheritdoc />
        public WindsorDependencyResolver(IWindsorContainer container)
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
            // No need for anything special to register as default, because "In Windsor first one wins":
            // https://github.com/castleproject/Windsor/blob/master/docs/registering-components-one-by-one.md
            container.Register(
                Component
                    .For<TService>()
                    .ImplementedBy<TImplementation>()
            );
        }

        /// <inheritdoc />
        public void RegisterDefault<TService>(Func<TService> factory) where TService : class
        {
            container.Register(
                Component
                    .For<TService>()
                    .UsingFactoryMethod(() => factory())
            );
        }


        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            container.Register(
                Component
                    .For<TService>()
                    .ImplementedBy<TImplementation>()
                    .LifestyleSingleton()
            );
        }

        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService>(TService instance) where TService : class
        {
            container.Register(
                Component
                    .For<TService>()
                    .Instance(instance)
            );
        }

        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService>(Func<TService> factory) where TService : class
        {
            container.Register(
                Component
                    .For<TService>()
                    .UsingFactoryMethod(() => factory())
                    .LifestyleSingleton()
            );
        }


        /// <inheritdoc />
        public void RegisterController(Type type)
        {
            container.Register(Component.For(type));
        }
    }
}
