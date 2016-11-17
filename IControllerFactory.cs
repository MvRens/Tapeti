using System;

namespace Tapeti
{
    public interface IControllerFactory
    {
        object CreateController(Type controllerType);
    }
}
