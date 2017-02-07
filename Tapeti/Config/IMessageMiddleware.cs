using System;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    public interface IMessageMiddleware
    {
        Task Handle(IMessageContext context, Func<Task> next);
    }
}
