using System.Threading.Tasks;
using Tapeti.Config;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Default
{
    /// <summary>
    /// Example exception strategy which requeues all messages that result in an error.
    /// </summary>
    /// <remarks>
    /// You probably do not want to use this strategy as-is in production code, unless
    /// you are sure that all your exceptions are transient. A better way would be to
    /// check for exceptions you know are transient. An even better way would be to
    /// never requeue but retry transient errors internally. See the Tapeti documentation
    /// for an example of this pattern:
    /// 
    /// https://tapeti.readthedocs.io/en/latest/
    /// </remarks>
    public class RequeueExceptionStrategy : IExceptionStrategy
    {
        /// <inheritdoc />
        public Task HandleException(IExceptionStrategyContext context)
        {
            context.SetConsumeResult(ConsumeResult.Requeue);
            return Task.CompletedTask;
        }
    }
}
