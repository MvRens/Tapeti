using System;

namespace Tapeti.Config
{
    public interface IMessageMiddleware
    {
        void Handle(IMessageContext context, Action next);
    }
}
