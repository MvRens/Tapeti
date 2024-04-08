using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Annotations;
using Tapeti.Config;
using Tapeti.Flow.Annotations;
using Tapeti.Helpers;

namespace Tapeti.Flow.Default
{
    #if !NET7_0_OR_GREATER
    #pragma warning disable CS1591
    public class UnreachableException : Exception
    {
        public UnreachableException(string message) : base(message)
        {
        }
    }
    #pragma warning restore CS1591
    #endif

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
                    if (value == null)
                        throw new InvalidOperationException("Return value should be a Task, not null");

                    await ((Task)value).ConfigureAwait(false);
                    await HandleParallelResponse(messageContext).ConfigureAwait(false);
                });
            }
            else if (context.Result.Info.ParameterType == typeof(ValueTask))
            {
                context.Result.SetHandler(async (messageContext, value) =>
                {
                    if (value == null)
                        // ValueTask is a struct and should never be null
                        throw new UnreachableException("Return value should be a ValueTask, not null");

                    await ((ValueTask)value).ConfigureAwait(false);
                    await HandleParallelResponse(messageContext).ConfigureAwait(false);
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
                    context.Result.SetHandler((messageContext, value) =>
                    {
                        if (value == null)
                            throw new InvalidOperationException("Return value should be an IYieldPoint, not null");

                        return HandleYieldPoint(messageContext, (IYieldPoint)value);
                    });
                    break;

                case TaskType.Task:
                    context.Result.SetHandler(async (messageContext, value) =>
                    {
                        if (value == null)
                            throw new InvalidOperationException("Return value should be a Task<IYieldPoint>, not null");

                        var yieldPoint = await ((Task<IYieldPoint>)value).ConfigureAwait(false);
                        await HandleYieldPoint(messageContext, yieldPoint).ConfigureAwait(false);
                    });
                    break;

                case TaskType.ValueTask:
                    context.Result.SetHandler(async (messageContext, value) =>
                    {
                        if (value == null)
                            // ValueTask is a struct and should never be null
                            throw new UnreachableException("Return value should be a ValueTask<IYieldPoint>, not null");

                        var yieldPoint = await ((ValueTask<IYieldPoint>)value).ConfigureAwait(false);
                        await HandleYieldPoint(messageContext, yieldPoint).ConfigureAwait(false);
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
            if (!context.TryGet<FlowMessageContextPayload>(out var flowPayload))
                return default;

            if (flowPayload.FlowIsConverging)
                return default;

            var flowHandler = context.Config.DependencyResolver.Resolve<IFlowHandler>();
            return flowHandler.Execute(new FlowHandlerContext(context), new DelegateYieldPoint(async flowContext =>
            {
                // IFlowParallelRequest.AddRequest will store the flow immediately
                if (!flowPayload.FlowContext.IsStoredOrDeleted())
                    await flowContext.Store(context.Binding.QueueType == QueueType.Durable).ConfigureAwait(false);
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


        private static object? ParallelRequestParameterFactory(IMessageContext context)
        {
            var flowHandler = context.Config.DependencyResolver.Resolve<IFlowHandler>();
            return flowHandler.GetParallelRequest(new FlowHandlerContext(context));
        }
    }
}
