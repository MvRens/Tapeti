using System;
using System.Collections.Generic;

namespace Tapeti.Tests.Mock
{
    public class MockDependencyResolver : IDependencyResolver
    {
        private readonly Dictionary<Type, object> container = new();


        public void Set<TInterface>(TInterface instance)
        {
            container.Add(typeof(TInterface), instance);
        }


        public T Resolve<T>() where T : class
        {
            return (T)Resolve(typeof(T));
        }


        public object Resolve(Type type)
        {
            return container[type];
        }
    }
}