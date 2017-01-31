using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Flow.Annotations;
using Tapeti.Flow.Default;

namespace Tapeti.Flow
{
    public class FlowMiddleware : IMiddlewareBundle
    {
        public IEnumerable<object> GetContents(IDependencyResolver dependencyResolver)
        {
            var container = dependencyResolver as IDependencyContainer;
            if (container != null)
            {
                container.RegisterDefault<IFlowProvider, FlowProvider>();
                container.RegisterDefault<IFlowHandler, FlowProvider>();
                // TODO singleton
                container.RegisterDefault<IFlowStore, FlowStore>();
                container.RegisterDefault<IFlowRepository, NonPersistentFlowRepository>();
            }

            return new[] { new FlowBindingMiddleware() };
        }


        internal class FlowBindingMiddleware : IBindingMiddleware
        {
            public void Handle(IBindingContext context, Action next)
            {
                HandleContinuationFilter(context);
                HandleYieldPointResult(context);

                next();
            }


            private static void HandleContinuationFilter(IBindingContext context)
            {
                var continuationAttribute = context.Method.GetCustomAttribute<ContinuationAttribute>();
                if (continuationAttribute != null)
                {
                    context.Use(new FlowBindingFilter());
                    context.Use(new FlowMessageMiddleware());
                }
            }


            private static void HandleYieldPointResult(IBindingContext context)
            {
                if (context.Result.Info.ParameterType == typeof(IYieldPoint))
                    context.Result.SetHandler((messageContext, value) => HandleYieldPoint(messageContext, (IYieldPoint)value));

                else if (context.Result.Info.ParameterType == typeof(Task<>))
                {
                    var genericArguments = context.Result.Info.ParameterType.GetGenericArguments();
                    if (genericArguments.Length == 1 && genericArguments[0] == typeof(IYieldPoint))
                        context.Result.SetHandler(async (messageContext, value) =>
                        {
                            var yieldPoint = await (Task<IYieldPoint>)value;
                            if (yieldPoint != null)
                                await HandleYieldPoint(messageContext, yieldPoint);
                        });
                }
            }


            private static Task HandleYieldPoint(IMessageContext context, IYieldPoint yieldPoint)
            {            
                var flowHandler = context.DependencyResolver.Resolve<IFlowHandler>();
                return flowHandler.Execute(context, yieldPoint);
            }
        }
    }
}
