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
        public void HandleException(IExceptionStrategyContext context)
        {
            context.SetConsumeResult(ConsumeResult.Error);
        }
    }
}
