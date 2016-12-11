using System;
using Tapeti.Config;

namespace Tapeti.Default
{
    public class MessageBinding : IBindingMiddleware
    {
        public void Handle(IBindingContext context, Action next)
        {
            if (context.Parameters.Count == 0)
                throw new TopologyConfigurationException("First parameter must be a message class");

            var parameter = context.Parameters[0];
            if (!parameter.Info.ParameterType.IsClass)
                throw new TopologyConfigurationException($"First parameter {parameter.Info.Name} must be a message class");

            parameter.SetBinding(messageContext => messageContext.Message);
            context.MessageClass = parameter.Info.ParameterType;

            next();
        }
    }
}
