using System;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Helpers;

namespace Tapeti.Flow.Default
{
    /// <inheritdoc cref="IControllerMessageMiddleware"/> />
    /// <summary>
    /// Handles methods marked with the Continuation attribute.
    /// </summary>
    internal class FlowContinuationMiddleware : IControllerFilterMiddleware, IControllerMessageMiddleware, IControllerCleanupMiddleware
    {
        public async ValueTask Filter(IMessageContext context, Func<ValueTask> next)
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


        public async ValueTask Handle(IMessageContext context, Func<ValueTask> next)
        {
            if (!context.TryGet<ControllerMessageContextPayload>(out var controllerPayload))
                return;

            if (context.TryGet<FlowMessageContextPayload>(out var flowPayload))
            {
                if (controllerPayload.Controller == null)
                    throw new InvalidOperationException("Controller is not available (method is static?)");

                var flowContext = flowPayload.FlowContext;
                if (!string.IsNullOrEmpty(flowContext.FlowState.Data))
                    Newtonsoft.Json.JsonConvert.PopulateObject(flowContext.FlowState.Data, controllerPayload.Controller);

                // Remove Continuation now because the IYieldPoint result handler will store the new state
                flowContext.FlowState.Continuations.Remove(flowContext.ContinuationID);

                await next();

                if (flowPayload.FlowIsConverging)
                {
                    var flowHandler = flowContext.HandlerContext.Config.DependencyResolver.Resolve<IFlowHandler>();
                    await flowHandler.Converge(new FlowHandlerContext(context));
                }
            }
            else
                await next();
        }


        public async ValueTask Cleanup(IMessageContext context, ConsumeResult consumeResult, Func<ValueTask> next)
        {
            await next();

            if (!context.TryGet<ControllerMessageContextPayload>(out var controllerPayload))
                return;

            if (!context.TryGet<FlowMessageContextPayload>(out var flowPayload))
                return;

            var flowContext = flowPayload.FlowContext;

            if (flowContext.ContinuationMetadata == null || flowContext.ContinuationMetadata.MethodName != MethodSerializer.Serialize(controllerPayload.Binding.Method))
                // Do not call when the controller method was filtered, if the same message has two methods
                return;

            if (flowContext.HasFlowStateAndLock)
            {
                if (!flowContext.IsStoredOrDeleted())
                    // The exception strategy can set the consume result to Success. Instead, check if the yield point
                    // was handled. The flow provider ensures we only end up here in case of an exception.
                    await flowContext.FlowStateLock.DeleteFlowState();

                flowContext.FlowStateLock.Dispose();
            }
        }



        private static async ValueTask<FlowContext?> EnrichWithFlowContext(IMessageContext context)
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

            var flowContext = new FlowContext(new FlowHandlerContext(context), flowState, flowStateLock)
            {
                ContinuationID = continuationID,
                ContinuationMetadata = flowState.Continuations.TryGetValue(continuationID, out var continuation) ? continuation : null
            };

            // IDisposable items in the IMessageContext are automatically disposed
            context.Store(new FlowMessageContextPayload(flowContext));
            return flowContext;
        }
    }
}
