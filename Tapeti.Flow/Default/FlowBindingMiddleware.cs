using System;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Flow.Annotations;
using Tapeti.Helpers;

namespace Tapeti.Flow.Default
{
    // TODO figure out a way to prevent binding on Continuation methods (which are always the target of a direct response)
    internal class FlowBindingMiddleware : IBindingMiddleware
    {
        public void Handle(IBindingContext context, Action next)
        {
            RegisterContinuationFilter(context);
            RegisterYieldPointResult(context);

            next();

            ValidateRequestResponse(context);
        }


        private static void RegisterContinuationFilter(IBindingContext context)
        {
            var continuationAttribute = context.Method.GetCustomAttribute<ContinuationAttribute>();
            if (continuationAttribute == null)
                return;

            context.Use(new FlowMessageFilterMiddleware());
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


        private static void ValidateRequestResponse(IBindingContext context)
        {
            var request = context.MessageClass?.GetCustomAttribute<RequestAttribute>();
            if (request?.Response == null)
                return;

            bool isTask;
            if (!context.Result.Info.ParameterType.IsTypeOrTaskOf(t => t == request.Response || t == typeof(IYieldPoint), out isTask))
                throw new ResponseExpectedException($"Response of class {request.Response.FullName} expected in controller {context.Method.DeclaringType?.FullName}, method {context.Method.Name}");
        }
    }
}
