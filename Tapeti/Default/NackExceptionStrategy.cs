using Tapeti.Config;

namespace Tapeti.Default
{
    public class NackExceptionStrategy : IExceptionStrategy
    {
        public void HandleException(IExceptionStrategyContext context)
        {
            context.HandlingResult.ConsumeResponse = ConsumeResponse.Nack;
        }
    }
}
