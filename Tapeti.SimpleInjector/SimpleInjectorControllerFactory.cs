using System;
using SimpleInjector;

namespace Tapeti.SimpleInjector
{
    public class SimpleInjectorControllerFactory : IControllerFactory
    {
        private readonly Container container;


        public SimpleInjectorControllerFactory(Container container)
        {
            this.container = container;
        }


        public object CreateController(Type controllerType)
        {
            return container.GetInstance(controllerType);
        }
    }
}
