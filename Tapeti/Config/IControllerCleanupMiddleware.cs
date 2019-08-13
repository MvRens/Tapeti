using System;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    /// <inheritdoc />
    /// <summary>
    /// Denotes middleware that runs after controller methods.
    /// </summary>
    public interface IControllerCleanupMiddleware : IControllerMiddlewareBase
    {
        /// <summary>
        /// Called after the message handler method, even if exceptions occured.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="handlingResult"></param>
        /// <param name="next">Always call to allow the next in the chain to clean up</param>
        Task Cleanup(IControllerMessageContext context, HandlingResult handlingResult, Func<Task> next);
    }
}
