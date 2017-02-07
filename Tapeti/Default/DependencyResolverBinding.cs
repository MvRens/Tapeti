using System;
using System.Linq;
using Tapeti.Config;

namespace Tapeti.Default
{
    public class DependencyResolverBinding : IBindingMiddleware
    {
        public void Handle(IBindingContext context, Action next)
        {
            next();

            foreach (var parameter in context.Parameters.Where(p => !p.HasBinding && p.Info.ParameterType.IsClass))
                parameter.SetBinding(messageContext => messageContext.DependencyResolver.Resolve(parameter.Info.ParameterType));
        }
    }
}
