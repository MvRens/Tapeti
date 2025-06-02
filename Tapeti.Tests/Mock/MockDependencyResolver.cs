using System;
using System.Collections.Generic;

namespace Tapeti.Tests.Mock
{
    public class MockDependencyResolver : IDependencyContainer
    {
        private readonly Dictionary<Type, Func<object>> container = new();


        public void Set<TInterface>(TInterface instance) where TInterface : class
        {
            container.Add(typeof(TInterface), () => instance);
        }


        public T Resolve<T>() where T : class
        {
            return (T)Resolve(typeof(T));
        }


        public object Resolve(Type type)
        {
            return container[type]();
        }


        public void RegisterDefault<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            container.TryAdd(typeof(TService), Activator.CreateInstance<TImplementation>);
        }


        public void RegisterDefault<TService>(Func<TService> factory) where TService : class
        {
            container.TryAdd(typeof(TService), factory);
        }


        public void RegisterDefaultSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            var singletonFactory = new Lazy<TImplementation>(Activator.CreateInstance<TImplementation>);
            container.TryAdd(typeof(TService), () => singletonFactory.Value);
        }


        public void RegisterDefaultSingleton<TService>(TService instance) where TService : class
        {
            var singletonFactory = new Lazy<TService>(Activator.CreateInstance<TService>);
            container.TryAdd(typeof(TService), () => singletonFactory.Value);
        }


        public void RegisterDefaultSingleton<TService>(Func<TService> factory) where TService : class
        {
            var singletonFactory = new Lazy<TService>(factory);
            container.TryAdd(typeof(TService), () => singletonFactory.Value);
        }


        public void RegisterController(Type type)
        {
            container.TryAdd(type, () => Activator.CreateInstance(type)!);
        }
    }
}
