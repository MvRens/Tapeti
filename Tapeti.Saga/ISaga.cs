using System;

namespace Tapeti.Saga
{
    public interface ISaga<out T> : IDisposable where T : class
    {
        string Id { get;  }
        T State { get; }

        void ExpectResponse(string callId);
        void ResolveResponse(string callId);
    }
}
