using System;
using System.Linq;
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

            if (context.Method.IsStatic)
                throw new ArgumentException($"Continuation attribute is not valid on static methods in controller {context.Method.DeclaringType?.FullName}, method {context.Method.Name}");

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
            if (context.Result.Info.ParameterType == typeof(ValueTask))
            {
                context.Result.SetHandler(async (messageContext, value) =>
                {
                    await (ValueTask)value;
                    await HandleParallelResponse(messageContext);
                });
            }
            else if (context.Result.Info.ParameterType == typeof(void))
            {
                context.Result.SetHandler((messageContext, _) => HandleParallelResponse(messageContext));
            }
            else
                throw new ArgumentException($"Result type must be IYieldPoint, Task or void in controller {context.Method.DeclaringType?.FullName}, method {context.Method.Name}");


            foreach (var parameter in context.Parameters.Where(p => !p.HasBinding && p.Info.ParameterType == typeof(IFlowParallelRequest)))
                parameter.SetBinding(ParallelRequestParameterFactory);
        }


        private static void RegisterYieldPointResult(IControllerBindingContext context)
        {
            if (!context.Result.Info.ParameterType.IsTypeOrTaskOf(typeof(IYieldPoint), out var taskType))
                return;

            if (context.Method.IsStatic)
                throw new ArgumentException($"Yield points are not valid on static methods in controller {context.Method.DeclaringType?.FullName}, method {context.Method.Name}");

            switch (taskType)
            {
                case TaskType.None:
                    context.Result.SetHandler((messageContext, value) => HandleYieldPoint(messageContext, (IYieldPoint)value));
                    break;

                case TaskType.Task:
                    context.Result.SetHandler(async (messageContext, value) =>
                    {
                        var yieldPoint = await (Task<IYieldPoint>)value;
                        if (yieldPoint != null)
                            await HandleYieldPoint(messageContext, yieldPoint);
                    });
                    break;

                case TaskType.ValueTask:
                    context.Result.SetHandler(async (messageContext, value) =>
                    {
                        var yieldPoint = await (ValueTask<IYieldPoint>)value;
                        if (yieldPoint != null)
                            await HandleYieldPoint(messageContext, yieldPoint);
                    });
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        private static ValueTask HandleYieldPoint(IMessageContext context, IYieldPoint yieldPoint)
        {
            var flowHandler = context.Config.DependencyResolver.Resolve<IFlowHandler>();
            return flowHandler.Execute(new FlowHandlerContext(context), yieldPoint);
        }


        private static ValueTask HandleParallelResponse(IMessageContext context)
        {
            if (context.TryGet<FlowMessageContextPayload>(out var flowPayload) && flowPayload.FlowIsConverging)
                return default;

            var flowHandler = context.Config.DependencyResolver.Resolve<IFlowHandler>();
            return flowHandler.Execute(new FlowHandlerContext(context), new DelegateYieldPoint(async flowContext =>
            {
                await flowContext.Store(context.Binding.QueueType == QueueType.Durable);
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


        private static object ParallelRequestParameterFactory(IMessageContext context)
        {
            var flowHandler = context.Config.DependencyResolver.Resolve<IFlowHandler>();
            return flowHandler.GetParallelRequest(new FlowHandlerContext(context));
        }
    }
}
