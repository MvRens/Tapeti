using System;
using System.Linq;
using Tapeti.Config;

namespace Tapeti.Saga
{
    public class SagaBindingMiddleware : IBindingMiddleware
    {
        public void Handle(IBindingContext context, Action next)
        {
            foreach (var parameter in context.Parameters.Where(p => 
                p.Info.ParameterType.IsGenericType &&
                p.Info.ParameterType.GetGenericTypeDefinition() == typeof(ISaga<>)))
            {
                parameter.SetBinding(messageContext =>
                {
                    object saga;
                    if (!messageContext.Items.TryGetValue("Saga", out saga))
                        return null;

                    return saga.GetType() == typeof(ISaga<>) ? saga : null;
                });
            }

            next();
        }
    }
}
