using System;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Tests
{
    public class TransientFilterMiddleware : IMessageFilterMiddleware
    {
        public Task Handle(IMessageContext context, Func<Task> next)
        {
            throw new NotImplementedException();
        }
    }
}