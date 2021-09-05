using System;
using Tapeti.Cmd.ConsoleHelper;

namespace Tapeti.Cmd.RateLimiter
{
    public static class RateLimiterFactory
    {
        public static IRateLimiter Create(IConsole console, int? maxRate, int? batchSize, int? batchPauseTime)
        {
            IRateLimiter rateLimiter;

            if (maxRate > 0)
                rateLimiter = new SpreadRateLimiter(maxRate.Value, TimeSpan.FromSeconds(1));
            else
                rateLimiter = new NoRateLimiter();

            // ReSharper disable once InvertIf - I don't like the readability of that flow
            if (batchSize > 0)
            {
                if (batchPauseTime > 0)
                    rateLimiter = new TimedBatchSizeRateLimiter(console, rateLimiter, batchSize.Value, batchPauseTime.Value);
                else
                    rateLimiter = new ManualBatchSizeRateLimiter(console, rateLimiter, batchSize.Value);
            }

            return rateLimiter;
        }
    }
}
