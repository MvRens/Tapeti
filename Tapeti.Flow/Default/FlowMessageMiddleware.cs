using System;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Flow.Default
{
    public class FlowMessageMiddleware : IMessageMiddleware
    {
        public async Task Handle(IMessageContext context, Func<Task> next)
        {
            var flowContext = (FlowContext)context.Items[ContextItems.FlowContext];
            if (flowContext != null)
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


        private static async Task CallConvergeMethod(IMessageContext context, string methodName, bool sync)
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

            var flowHandler = context.DependencyResolver.Resolve<IFlowHandler>();
            await flowHandler.Execute(context, yieldPoint);
        }
    }
}
