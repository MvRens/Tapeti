using System;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    public interface IPublishMiddleware
    {
        Task Handle(IPublishContext context, Func<Task> next);
    }
}
