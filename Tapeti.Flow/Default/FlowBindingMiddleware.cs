using System;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Flow.Annotations;
using Tapeti.Helpers;

namespace Tapeti.Flow.Default
{
    internal class FlowBindingMiddleware : IBindingMiddleware
    {
        public void Handle(IBindingContext context, Action next)
        {
            if (context.Method.GetCustomAttribute<StartAttribute>() != null)
                return;

            if (context.Method.GetCustomAttribute<ContinuationAttribute>() != null)
                context.QueueBindingMode = QueueBindingMode.DirectToQueue;

            RegisterYieldPointResult(context);
            RegisterContinuationFilter(context);

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


        private static void RegisterYieldPointResult(IBindingContext context)
        {
            bool isTaskOf;
            if (!context.Result.Info.ParameterType.IsTypeOrTaskOf(typeof(IYieldPoint), out isTaskOf))
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


        private static Task HandleYieldPoint(IMessageContext context, IYieldPoint yieldPoint)
        {
            var flowHandler = context.DependencyResolver.Resolve<IFlowHandler>();
            return flowHandler.Execute(context, yieldPoint);
        }


        private static Task HandleParallelResponse(IMessageContext context)
        {
            var flowHandler = context.DependencyResolver.Resolve<IFlowHandler>();
            return flowHandler.Execute(context, new DelegateYieldPoint((a) => Task.CompletedTask));
        }


        private static void ValidateRequestResponse(IBindingContext context)
        {
            var request = context.MessageClass?.GetCustomAttribute<RequestAttribute>();
            if (request?.Response == null)
                return;

            bool isTaskOf;
            if (!context.Result.Info.ParameterType.IsTypeOrTaskOf(t => t == request.Response || t == typeof(IYieldPoint), out isTaskOf))
                throw new ResponseExpectedException($"Response of class {request.Response.FullName} expected in controller {context.Method.DeclaringType?.FullName}, method {context.Method.Name}");
        }
    }
}
