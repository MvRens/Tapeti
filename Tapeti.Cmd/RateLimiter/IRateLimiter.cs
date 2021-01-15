using System;

namespace Tapeti.Cmd.RateLimiter
{
    public interface IRateLimiter
    {
        void Execute(Action action);
    }
}
