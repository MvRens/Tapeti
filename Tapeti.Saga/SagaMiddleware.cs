using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Saga
{
    public class SagaMiddleware : IMiddlewareBundle
    {
        private const string SagaContextKey = "Saga";


        public IEnumerable<object> GetContents(IDependencyResolver dependencyResolver)
        {
            (dependencyResolver as IDependencyInjector)?.RegisterDefault<ISagaProvider, SagaProvider>();

            yield return new SagaBindingMiddleware();
        }
   

        protected class SagaBindingMiddleware : IBindingMiddleware
        {
            public void Handle(IBindingContext context, Action next)
            {
                var registered = false;

                foreach (var parameter in context.Parameters.Where(p =>
                    p.Info.ParameterType.IsGenericType &&
                    p.Info.ParameterType.GetGenericTypeDefinition() == typeof(ISaga<>)))
                {
                    if (!registered)
                    {
                        var sagaType = parameter.Info.ParameterType.GetGenericArguments()[0];
                        var middlewareType = typeof(SagaMessageMiddleware<>).MakeGenericType(sagaType);

                        context.Use(Activator.CreateInstance(middlewareType) as IMessageMiddleware);
                        
                        registered = true;
                    }

                    parameter.SetBinding(messageContext =>
                    {
                        object saga;
                        return messageContext.Items.TryGetValue(SagaContextKey, out saga) ? saga : null;
                    });
                }

                next();
            }
        }


        protected class SagaMessageMiddleware<T> : IMessageMiddleware where T : class
        {
            public async Task Handle(IMessageContext context, Func<Task> next)
            {
                if (string.IsNullOrEmpty(context.Properties.CorrelationId))
                    return;

                var saga = await context.DependencyResolver.Resolve<ISagaProvider>().Continue<T>(context.Properties.CorrelationId);
                if (saga == null)
                    return;

                context.Items[SagaContextKey] = saga;
                await next();
            }
        }
    }
}
