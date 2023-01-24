using System;

namespace Tapeti.Config
{
    /// <summary>
    /// Called when a Controller method is registered.
    /// </summary>
    public interface IControllerBindingMiddleware : IControllerMiddlewareBase
    {
        /// <summary>
        /// Called before a Controller method is registered. Can change the way parameters and return values are handled,
        /// and can inject message middleware specific to a method.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next">Must be called to activate the new layer of middleware.</param>
        void Handle(IControllerBindingContext context, Action next);
    }
}
