using System;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <inheritdoc />
    /// <summary>
    /// Gets the message class from the first parameter of a controller method.
    /// This middleware is included by default in the standard TapetiConfig.
    /// </summary>
    public class MessageBinding : IControllerBindingMiddleware
    {
        /// <inheritdoc />
        public void Handle(IControllerBindingContext context, Action next)
        {
            if (context.Parameters.Count == 0)
                throw new TopologyConfigurationException($"First parameter of method {context.Method.Name} in controller {context.Method.DeclaringType?.Name} must be a message class");

            var parameter = context.Parameters[0];
            if (!parameter.Info.ParameterType.IsClass)
                throw new TopologyConfigurationException($"First parameter {parameter.Info.Name} of method {context.Method.Name} in controller {context.Method.DeclaringType?.Name} must be a message class");

            parameter.SetBinding(messageContext => messageContext.Message);
            context.SetMessageClass(parameter.Info.ParameterType);

            next();
        }
    }
}
