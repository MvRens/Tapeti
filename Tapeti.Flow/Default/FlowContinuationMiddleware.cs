using System;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Flow.Default
{
    /// <inheritdoc cref="IControllerMessageMiddleware"/> />
    /// <summary>
    /// Handles methods marked with the Continuation attribute.
    /// </summary>
    internal class FlowContinuationMiddleware : IControllerFilterMiddleware, IControllerMessageMiddleware, IControllerCleanupMiddleware
    {
        public async Task Filter(IMessageContext context, Func<Task> next)
        {
            if (!context.TryGet<ControllerMessageContextPayload>(out var controllerPayload))
                return;

            var flowContext = await EnrichWithFlowContext(context);
            if (flowContext?.ContinuationMetadata == null)
                return;

            if (flowContext.ContinuationMetadata.MethodName != MethodSerializer.Serialize(controllerPayload.Binding.Method))
                return;

            await next();
        }


        public async Task Handle(IMessageContext context, Func<Task> next)
        {
            if (!context.TryGet<ControllerMessageContextPayload>(out var controllerPayload))
                return;

            if (context.TryGet<FlowMessageContextPayload>(out var flowPayload))
            {
                var flowContext = flowPayload.FlowContext;
                Newtonsoft.Json.JsonConvert.PopulateObject(flowContext.FlowState.Data, controllerPayload.Controller);

                // Remove Continuation now because the IYieldPoint result handler will store the new state
                flowContext.FlowState.Continuations.Remove(flowContext.ContinuationID);
                var converge = flowContext.FlowState.Continuations.Count == 0 &&
                               flowContext.ContinuationMetadata.ConvergeMethodName != null;

                if (converge)
                    // Indicate to the FlowBindingMiddleware that the state must not to be stored
                    flowPayload.FlowIsConverging = true;

                await next();

                if (converge)
                    await CallConvergeMethod(context, controllerPayload,
                        flowContext.ContinuationMetadata.ConvergeMethodName,
                        flowContext.ContinuationMetadata.ConvergeMethodSync);
            }
            else
                await next();
        }


        public async Task Cleanup(IMessageContext context, ConsumeResult consumeResult, Func<Task> next)
        {
            await next();

            if (!context.TryGet<ControllerMessageContextPayload>(out var controllerPayload))
                return;

            if (!context.TryGet<FlowMessageContextPayload>(out var flowPayload))
                return;

            var flowContext = flowPayload.FlowContext;

            if (flowContext.ContinuationMetadata.MethodName != MethodSerializer.Serialize(controllerPayload.Binding.Method))
                // Do not call when the controller method was filtered, if the same message has two methods
                return;

            if (flowContext.FlowStateLock != null)
            {
                if (!flowContext.IsStoredOrDeleted())
                    // The exception strategy can set the consume result to Success. Instead, check if the yield point
                    // was handled. The flow provider ensures we only end up here in case of an exception.
                    await flowContext.FlowStateLock.DeleteFlowState();

                flowContext.FlowStateLock.Dispose();
            }
        }



        private static async Task<FlowContext> EnrichWithFlowContext(IMessageContext context)
        {
            if (context.TryGet<FlowMessageContextPayload>(out var flowPayload))
                return flowPayload.FlowContext;


            if (context.Properties.CorrelationId == null)
                return null;

            if (!Guid.TryParse(context.Properties.CorrelationId, out var continuationID))
                return null;

            var flowStore = context.Config.DependencyResolver.Resolve<IFlowStore>();

            var flowID = await flowStore.FindFlowID(continuationID);
            if (!flowID.HasValue)
                return null;

            var flowStateLock = await flowStore.LockFlowState(flowID.Value);

            var flowState = await flowStateLock.GetFlowState();
            if (flowState == null)
                return null;

            var flowContext = new FlowContext
            {
                HandlerContext = new FlowHandlerContext(context),

                FlowStateLock = flowStateLock,
                FlowState = flowState,

                ContinuationID = continuationID,
                ContinuationMetadata = flowState.Continuations.TryGetValue(continuationID, out var continuation) ? continuation : null
            };

            // IDisposable items in the IMessageContext are automatically disposed
            context.Store(new FlowMessageContextPayload(flowContext));
            return flowContext;
        }


        private static async Task CallConvergeMethod(IMessageContext context, ControllerMessageContextPayload controllerPayload, string methodName, bool sync)
        {
            IYieldPoint yieldPoint;
            
            

            var method = controllerPayload.Controller.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                throw new ArgumentException($"Unknown converge method in controller {controllerPayload.Controller.GetType().Name}: {methodName}");

            if (sync)
                yieldPoint = (IYieldPoint)method.Invoke(controllerPayload.Controller, new object[] {});
            else
                yieldPoint = await (Task<IYieldPoint>)method.Invoke(controllerPayload.Controller, new object[] { });

            if (yieldPoint == null)
                throw new YieldPointException($"Yield point is required in controller {controllerPayload.Controller.GetType().Name} for converge method {methodName}");

            var flowHandler = context.Config.DependencyResolver.Resolve<IFlowHandler>();
            await flowHandler.Execute(new FlowHandlerContext(context), yieldPoint);
        }
    }
}
