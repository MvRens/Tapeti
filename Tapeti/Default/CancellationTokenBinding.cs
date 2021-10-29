using System;
using System.Linq;
using System.Threading;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <inheritdoc />
    /// <summary>
    /// Binds a parameter of type CancellationToken to a token which is cancelled when the RabbitMQ connection is closed.
    /// Similar to and very much inspired by ASP.NET's RequestAborted CancellationToken.
    /// This middleware is included by default in the standard TapetiConfig.
    /// </summary>
    public class CancellationTokenBinding : IControllerBindingMiddleware
    {
        /// <inheritdoc />
        public void Handle(IControllerBindingContext context, Action next)
        {
            foreach (var parameter in context.Parameters.Where(p => !p.HasBinding && p.Info.ParameterType == typeof(CancellationToken)))
                parameter.SetBinding(messageContext => messageContext.ConnectionClosed);

            next();
        }
    }
}
