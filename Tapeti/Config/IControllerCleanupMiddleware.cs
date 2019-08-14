using System;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    /// <summary>
    /// Denotes middleware that runs after controller methods.
    /// </summary>
    public interface IControllerCleanupMiddleware
    {
        /// <summary>
        /// Called after the message handler method, even if exceptions occured.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="consumeResult"></param>
        /// <param name="next">Always call to allow the next in the chain to clean up</param>
        Task Cleanup(IMessageContext context, ConsumeResult consumeResult, Func<Task> next);
    }
}
