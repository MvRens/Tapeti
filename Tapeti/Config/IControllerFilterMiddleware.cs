using System;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    /// <summary>
    /// Denotes middleware that runs before the controller is instantiated.
    /// </summary>
    public interface IControllerFilterMiddleware : IControllerMiddlewareBase
    {
        /// <summary>
        /// Called before the 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        ValueTask Filter(IMessageContext context, Func<ValueTask> next);
    }
}
