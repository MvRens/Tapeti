using System;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    /// <summary>
    /// Denotes middleware that runs for controller methods.
    /// </summary>
    public interface IControllerMessageMiddleware : IControllerMiddlewareBase
    {
        /// <summary>
        /// Called after the message has passed any filter middleware and the controller has been instantiated,
        /// but before the method has been called.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next">Call to pass the message to the next handler in the chain or call the controller method</param>
        Task Handle(IMessageContext context, Func<Task> next);
    }
}
