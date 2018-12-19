using System;
using System.Threading.Tasks;

// ReSharper disable UnusedMember.Global

namespace Tapeti
{
    public interface IConnection : IDisposable
    {
        Task<ISubscriber> Subscribe();
    }
}
