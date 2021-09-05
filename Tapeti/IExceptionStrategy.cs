using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti
{
    /// <summary>
    /// Called when an exception occurs while handling a message. Determines how it should be handled.
    /// </summary>
    public interface IExceptionStrategy
    {
        /// <summary>
        /// Called when an exception occurs while handling a message.
        /// </summary>
        /// <param name="context">The exception strategy context containing the necessary data including the message context and the thrown exception.
        /// Also proivdes methods for the exception strategy to indicate how the message should be handled.</param>
        Task HandleException(IExceptionStrategyContext context);
    }
}
