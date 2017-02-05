using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Flow.Annotations;
using Tapeti.Flow.Default;
using Tapeti.Helpers;

namespace Tapeti.Flow
{
    public class FlowMiddleware : IMiddlewareBundle
    {
        public IEnumerable<object> GetContents(IDependencyResolver dependencyResolver)
        {
            var container = dependencyResolver as IDependencyContainer;

            // ReSharper disable once InvertIf
            if (container != null)
            {
                container.RegisterDefault<IFlowProvider, FlowProvider>();
                container.RegisterDefault<IFlowHandler, FlowProvider>();
                container.RegisterDefault<IFlowRepository, NonPersistentFlowRepository>();
                container.RegisterDefault<IFlowStore, FlowStore>();
            }

            return new[] { new FlowBindingMiddleware() };
        }


        internal class FlowBindingMiddleware : IBindingMiddleware
        {
            public void Handle(IBindingContext context, Action next)
            {
                RegisterContinuationFilter(context);
                RegisterYieldPointResult(context);

                next();
            }


            private static void RegisterContinuationFilter(IBindingContext context)
            {
                var continuationAttribute = context.Method.GetCustomAttribute<ContinuationAttribute>();
                if (continuationAttribute == null)
                    return;

                context.Use(new FlowBindingFilter());
                context.Use(new FlowMessageMiddleware());
            }


            private static void RegisterYieldPointResult(IBindingContext context)
            {
                bool isTask;
                if (!context.Result.Info.ParameterType.IsTypeOrTaskOf(typeof(IYieldPoint), out isTask))
                    return;

                if (isTask)
                {
                    context.Result.SetHandler(async (messageContext, value) =>
                    {
                        var yieldPoint = await (Task<IYieldPoint>)value;
                        if (yieldPoint != null)
                            await HandleYieldPoint(messageContext, yieldPoint);
                    });
                }
                else
                    context.Result.SetHandler((messageContext, value) => HandleYieldPoint(messageContext, (IYieldPoint)value));
            }


            private static Task HandleYieldPoint(IMessageContext context, IYieldPoint yieldPoint)
            {            
                var flowHandler = context.DependencyResolver.Resolve<IFlowHandler>();
                return flowHandler.Execute(context, yieldPoint);
            }
        }
    }
}
