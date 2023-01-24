using System;

namespace Tapeti
{
    /// <summary>
    /// Wrapper interface for an IoC container to allow dependency injection in Tapeti.
    /// </summary>
    public interface IDependencyResolver
    {
        /// <summary>
        /// Resolve an instance of T
        /// </summary>
        /// <typeparam name="T">The type to instantiate</typeparam>
        /// <returns>A new or singleton instance, depending on the registration</returns>
        T Resolve<T>() where T : class;

        /// <summary>
        /// Resolve an instance of T
        /// </summary>
        /// <param name="type">The type to instantiate</param>
        /// <returns>A new or singleton instance, depending on the registration</returns>
        object Resolve(Type type);
    }


    /// <summary>
    /// Allows registering controller classes into the IoC container. Also registers default implementations,
    /// so that the calling application may override these.
    /// </summary>
    /// <remarks>
    /// All implementations of IDependencyResolver should implement IDependencyContainer as well,
    /// otherwise all registrations of Tapeti components will have to be done manually by the application.
    /// </remarks>
    public interface IDependencyContainer : IDependencyResolver
    {
        /// <summary>
        /// Registers a default implementation in the IoC container. If an alternative implementation
        /// was registered before, it is not replaced.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        void RegisterDefault<TService, TImplementation>() where TService : class where TImplementation : class, TService;

        /// <summary>
        /// Registers a default implementation in the IoC container. If an alternative implementation
        /// was registered before, it is not replaced.
        /// </summary>
        /// <param name="factory"></param>
        /// <typeparam name="TService"></typeparam>
        void RegisterDefault<TService>(Func<TService> factory) where TService : class;


        /// <summary>
        /// Registers a default singleton implementation in the IoC container. If an alternative implementation
        /// was registered before, it is not replaced.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        void RegisterDefaultSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService;

        /// <summary>
        /// Registers a default singleton implementation in the IoC container. If an alternative implementation
        /// was registered before, it is not replaced.
        /// </summary>
        /// <param name="instance"></param>
        /// <typeparam name="TService"></typeparam>
        void RegisterDefaultSingleton<TService>(TService instance) where TService : class;

        /// <summary>
        /// Registers a default singleton implementation in the IoC container. If an alternative implementation
        /// was registered before, it is not replaced.
        /// </summary>
        /// <param name="factory"></param>
        /// <typeparam name="TService"></typeparam>
        void RegisterDefaultSingleton<TService>(Func<TService> factory) where TService : class;


        /// <summary>
        /// Registers a concrete controller class in the IoC container.
        /// </summary>
        /// <param name="type"></param>
        void RegisterController(Type type);
    }
}
