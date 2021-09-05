using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <inheritdoc />
    /// <summary>
    /// Default implementation of an exception strategy which marks the messages as Error.
    /// </summary>
    public class NackExceptionStrategy : IExceptionStrategy
    {
        /// <inheritdoc />
        public Task HandleException(IExceptionStrategyContext context)
        {
            context.SetConsumeResult(ConsumeResult.Error);
            return Task.CompletedTask;
        }
    }
}
