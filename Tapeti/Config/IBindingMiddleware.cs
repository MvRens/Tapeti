using System;

namespace Tapeti.Config
{
    public interface IBindingMiddleware
    {
        void Handle(IBindingContext context, Action next);
    }
}
