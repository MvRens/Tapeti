using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tapeti.Default
{
    public class DefaultControllerFactory : IControllerFactory
    {
        private readonly Dictionary<Type, Func<object>> controllerConstructors = new Dictionary<Type, Func<object>>();
        private readonly Func<IPublisher> publisherFactory;

        public DefaultControllerFactory(Func<IPublisher> publisherFactory)
        {
            this.publisherFactory = publisherFactory;
        }


        public object CreateController(Type controllerType)
        {
            Func<object> constructor;
            if (!controllerConstructors.TryGetValue(controllerType, out constructor))
                throw new ArgumentException($"Can not create unregistered controller {controllerType.FullName}");

            return constructor();
        }


        public void RegisterController(Type type)
        {
            controllerConstructors.Add(type, GetConstructor(type));
        }


        protected Func<object> GetConstructor(Type type)
        {
            var constructors = type.GetConstructors();

            ConstructorInfo publisherConstructor = null;
            ConstructorInfo emptyConstructor = null;

            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length > 0)
                {
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(IPublisher))
                        publisherConstructor = constructor;
                }
                else
                    emptyConstructor = constructor;
            }

            if (publisherConstructor != null)
                return () => publisherConstructor.Invoke(new object[] { publisherFactory() });

            if (emptyConstructor != null)
                return () => emptyConstructor.Invoke(null);

            throw new ArgumentException($"Unable to construct type {type.Name}, a parameterless constructor or one with only an IPublisher parameter is required");
        }
    }
}
