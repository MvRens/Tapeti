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
        public async Task Filter(IControllerMessageContext context, Func<Task> next)
        {
            var flowContext = await EnrichWithFlowContext(context);
            if (flowContext?.ContinuationMetadata == null)
                return;

            if (flowContext.ContinuationMetadata.MethodName != MethodSerializer.Serialize(context.Binding.Method))
                return;

            await next();
        }


        public async Task Handle(IControllerMessageContext context, Func<Task> next)
        {
            if (context.Get(ContextItems.FlowContext, out FlowContext flowContext))
            { 
                Newtonsoft.Json.JsonConvert.PopulateObject(flowContext.FlowState.Data, context.Controller);

                // Remove Continuation now because the IYieldPoint result handler will store the new state
                flowContext.FlowState.Continuations.Remove(flowContext.ContinuationID);
                var converge = flowContext.FlowState.Continuations.Count == 0 &&
                               flowContext.ContinuationMetadata.ConvergeMethodName != null;

                await next();

                if (converge)
                    await CallConvergeMethod(context,
                                             flowContext.ContinuationMetadata.ConvergeMethodName, 
                                             flowContext.ContinuationMetadata.ConvergeMethodSync);
            }
            else
                await next();
        }


        public async Task Cleanup(IMessageContext context, ConsumeResult consumeResult, Func<Task> next)
        {
            await next();

            if (!context.Get(ContextItems.FlowContext, out FlowContext flowContext))
                return;

            if (flowContext?.FlowStateLock != null)
            {
                if (consumeResult == ConsumeResult.Error)
                    await flowContext.FlowStateLock.DeleteFlowState();

                flowContext.FlowStateLock.Dispose();
            }
        }



        private static async Task<FlowContext> EnrichWithFlowContext(IControllerMessageContext context)
        {
            if (context.Get(ContextItems.FlowContext, out FlowContext flowContext))
                return flowContext;


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

            flowContext = new FlowContext
            {
                HandlerContext = new FlowHandlerContext(context),

                FlowStateLock = flowStateLock,
                FlowState = flowState,

                ContinuationID = continuationID,
                ContinuationMetadata = flowState.Continuations.TryGetValue(continuationID, out var continuation) ? continuation : null
            };

            // IDisposable items in the IMessageContext are automatically disposed
            context.Store(ContextItems.FlowContext, flowContext);
            return flowContext;
        }


        private static async Task CallConvergeMethod(IControllerMessageContext context, string methodName, bool sync)
        {
            IYieldPoint yieldPoint;

            var method = context.Controller.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                throw new ArgumentException($"Unknown converge method in controller {context.Controller.GetType().Name}: {methodName}");

            if (sync)
                yieldPoint = (IYieldPoint)method.Invoke(context.Controller, new object[] {});
            else
                yieldPoint = await (Task<IYieldPoint>)method.Invoke(context.Controller, new object[] { });

            if (yieldPoint == null)
                throw new YieldPointException($"Yield point is required in controller {context.Controller.GetType().Name} for converge method {methodName}");

            var flowHandler = context.Config.DependencyResolver.Resolve<IFlowHandler>();
            await flowHandler.Execute(new FlowHandlerContext(context), yieldPoint);
        }
    }
}
