using System;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    /// <summary>
    /// Denotes middleware that processes all published messages.
    /// </summary>
    public interface IPublishMiddleware
    {
        /// <summary>
        /// Called when a message is published.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next">Call to pass the message to the next handler in the chain</param>
        Task Handle(IPublishContext context, Func<Task> next);
    }
}
