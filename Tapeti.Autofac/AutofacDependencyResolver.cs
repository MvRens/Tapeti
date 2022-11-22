using System;
using Autofac;
using Autofac.Builder;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Autofac
{
    /// <summary>
    /// Dependency resolver and container implementation for Autofac.
    /// Since this class needs access to both the ContainerBuilder and the built IContainer,
    /// either let AutofacDependencyResolver build the container by calling it's Build method,
    /// or set the Container property manually.
    /// </summary>
    public class AutofacDependencyResolver : IDependencyContainer
    {
        private ContainerBuilder containerBuilder;
        private IContainer container;


        /// <summary>
        /// The built container. Either set directly, or use the Build method to built the
        /// update this reference.
        /// </summary>
        public IContainer Container
        {
            get => container;
            set
            {
                container = value;
                if (value != null)
                    containerBuilder = null;
            }
        }


        /// <summary>
        /// </summary>
        public AutofacDependencyResolver(ContainerBuilder containerBuilder)
        {
            this.containerBuilder = containerBuilder;
        }


        /// <summary>
        /// Builds the container, updates the Container property and returns the newly built IContainer.
        /// </summary>
        public IContainer Build(ContainerBuildOptions options = ContainerBuildOptions.None)
        {
            CheckContainerBuilder();
            Container = containerBuilder.Build(options);

            return container;
        }


        /// <inheritdoc />
        public T Resolve<T>() where T : class
        {
            CheckContainer();
            return Container.Resolve<T>();
        }

        /// <inheritdoc />
        public object Resolve(Type type)
        {
            CheckContainer();
            return Container.Resolve(type);
        }


        /// <inheritdoc />
        public void RegisterDefault<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            CheckContainerBuilder();
            containerBuilder
                .RegisterType<TImplementation>()
                .As<TService>()
                .PreserveExistingDefaults();
        }

        /// <inheritdoc />
        public void RegisterDefault<TService>(Func<TService> factory) where TService : class
        {
            CheckContainerBuilder();
            containerBuilder
                .Register(_ => factory())
                .As<TService>()
                .PreserveExistingDefaults();
        }


        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            CheckContainerBuilder();
            containerBuilder
                .RegisterType<TImplementation>()
                .As<TService>()
                .SingleInstance()
                .PreserveExistingDefaults();
        }

        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService>(TService instance) where TService : class
        {
            CheckContainerBuilder();
            containerBuilder
                .RegisterInstance(instance)
                .As<TService>()
                .SingleInstance()
                .PreserveExistingDefaults();
        }

        /// <inheritdoc />
        public void RegisterDefaultSingleton<TService>(Func<TService> factory) where TService : class
        {
            CheckContainerBuilder();
            containerBuilder
                .Register(_ => factory())
                .As<TService>()
                .SingleInstance()
                .PreserveExistingDefaults();
        }


        /// <inheritdoc />
        public void RegisterController(Type type)
        {
            CheckContainerBuilder();
            containerBuilder
                .RegisterType(type)
                .AsSelf();
        }


        private void CheckContainer()
        {
            if (container == null)
                throw new InvalidOperationException("Container property has not been set yet on AutofacDependencyResolver");
        }


        private void CheckContainerBuilder()
        {
            if (containerBuilder == null)
                throw new InvalidOperationException("Container property has already been set on AutofacDependencyResolver");
        }
    }
}
