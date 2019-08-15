using System;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Annotations;
using Tapeti.Config;
using Tapeti.Flow.Annotations;
using Tapeti.Helpers;

namespace Tapeti.Flow.Default
{
    internal class FlowBindingMiddleware : IControllerBindingMiddleware
    {
        public void Handle(IControllerBindingContext context, Action next)
        {
            if (context.Method.GetCustomAttribute<StartAttribute>() != null)
                return;

            RegisterYieldPointResult(context);
            RegisterContinuationFilter(context);

            next();

            ValidateRequestResponse(context);
        }


        private static void RegisterContinuationFilter(IControllerBindingContext context)
        {
            var continuationAttribute = context.Method.GetCustomAttribute<ContinuationAttribute>();
            if (continuationAttribute == null)
                return;

            context.SetBindingTargetMode(BindingTargetMode.Direct);
            context.Use(new FlowContinuationMiddleware());

            if (context.Result.HasHandler)
                return;

            // Continuation without IYieldPoint indicates a ParallelRequestBuilder response handler,
            // make sure to store it's state as well
            if (context.Result.Info.ParameterType == typeof(Task))
            {
                context.Result.SetHandler(async (messageContext, value) =>
                {
                    await (Task)value;
                    await HandleParallelResponse(messageContext);
                });
            }
            else if (context.Result.Info.ParameterType == typeof(void))
            {
                context.Result.SetHandler((messageContext, value) => HandleParallelResponse(messageContext));
            }
            else
                throw new ArgumentException($"Result type must be IYieldPoint, Task or void in controller {context. Method.DeclaringType?.FullName}, method {context.Method.Name}");
        }


        private static void RegisterYieldPointResult(IControllerBindingContext context)
        {
            if (!context.Result.Info.ParameterType.IsTypeOrTaskOf(typeof(IYieldPoint), out var isTaskOf))
                return;

            if (isTaskOf)
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


        private static Task HandleYieldPoint(IControllerMessageContext context, IYieldPoint yieldPoint)
        {
            var flowHandler = context.Config.DependencyResolver.Resolve<IFlowHandler>();
            return flowHandler.Execute(new FlowHandlerContext(context), yieldPoint);
        }


        private static Task HandleParallelResponse(IControllerMessageContext context)
        {
            var flowHandler = context.Config.DependencyResolver.Resolve<IFlowHandler>();
            return flowHandler.Execute(new FlowHandlerContext(context), new DelegateYieldPoint(async flowContext =>
            {
                await flowContext.Store();
            }));
        }


        private static void ValidateRequestResponse(IControllerBindingContext context)
        {
            var request = context.MessageClass?.GetCustomAttribute<RequestAttribute>();
            if (request?.Response == null)
                return;

            if (!context.Result.Info.ParameterType.IsTypeOrTaskOf(t => t == request.Response || t == typeof(IYieldPoint), out _))
                throw new ResponseExpectedException($"Response of class {request.Response.FullName} expected in controller {context.Method.DeclaringType?.FullName}, method {context.Method.Name}");
        }
    }
}
