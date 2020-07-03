using System;

namespace Tapeti.Cmd.RateLimiter
{
    public class NoRateLimiter : IRateLimiter
    {
        public void Execute(Action action)
        {
            action();
        }
    }
}
