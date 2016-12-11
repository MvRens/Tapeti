using System;
using System.Linq;
using Tapeti.Config;

namespace Tapeti.Default
{
    public class DependencyResolverBinding : IBindingMiddleware
    {
        private readonly IDependencyResolver resolver;


        public DependencyResolverBinding(IDependencyResolver resolver)
        {
            this.resolver = resolver;
        }


        public void Handle(IBindingContext context, Action next)
        {
            next();

            foreach (var parameter in context.Parameters.Where(p => !p.HasBinding && p.Info.ParameterType.IsClass))
                parameter.SetBinding(messageContext => resolver.Resolve(parameter.Info.ParameterType));
        }
    }
}
