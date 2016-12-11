using System;
using System.Threading.Tasks;

namespace Tapeti
{
    public interface IConnection : IDisposable
    {
        Task<ISubscriber> Subscribe();
    }
}
