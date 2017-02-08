using System;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    public interface IMessageFilterMiddleware
    {
        Task Handle(IMessageContext context, Func<Task> next);
    }
}
