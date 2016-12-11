using System;
using Tapeti.Config;

namespace Tapeti.Default
{
    // End of the line...
    public class BindingBufferStop : IBindingMiddleware
    {
        public void Handle(IBindingContext context, Action next)
        {
        }
    }
}
