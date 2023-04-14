using System;
using System.Linq;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <summary>
    /// Attempts to resolve any unhandled parameters to Controller methods using the IoC container.
    /// This middleware is included by default in the standard TapetiConfig.
    /// </summary>
    public class DependencyResolverBinding : IControllerBindingMiddleware
    {
        /// <inheritdoc />
        public void Handle(IControllerBindingContext context, Action next)
        {
            next();

            foreach (var parameter in context.Parameters.Where(p => p is { HasBinding: false, Info.ParameterType.IsClass: true }))
                parameter.SetBinding(messageContext => messageContext.Config.DependencyResolver.Resolve(parameter.Info.ParameterType));
        }
    }
}
