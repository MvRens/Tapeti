using System;

namespace Tapeti
{
    public interface IConnection : IDisposable
    {
        ISubscriber Subscribe();
    }
}
