using System;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    /// <summary>
    /// Denotes middleware that processes all messages.
    /// </summary>
    public interface IMessageMiddleware
    {
        /// <summary>
        /// Called for all bindings when a message needs to be handled.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next">Call to pass the message to the next handler in the chain</param>
        Task Handle(IMessageContext context, Func<Task> next);
    }
}
