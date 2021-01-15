using System;
using System.Threading;

namespace Tapeti.Cmd.RateLimiter
{
    public class SpreadRateLimiter : IRateLimiter
    {
        private readonly TimeSpan delay;
        private DateTime lastExecute = DateTime.MinValue;

        public SpreadRateLimiter(int amount, TimeSpan perTimespan)
        {
            delay = TimeSpan.FromMilliseconds(perTimespan.TotalMilliseconds / amount);
        }


        public void Execute(Action action)
        {
            // Very simple implementation; the time between actions must be at least the delay.
            // This prevents bursts followed by nothing which are common with normal rate limiter implementations.
            var remainingWaitTime = delay - (DateTime.Now - lastExecute);

            if (remainingWaitTime.TotalMilliseconds > 0)
                Thread.Sleep(remainingWaitTime);

            action();
            lastExecute = DateTime.Now;
        }
    }
}
