using System;
using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    internal class DelegateYieldPoint : IYieldPoint
    {
        private readonly Func<FlowContext, Task> onExecute;


        public DelegateYieldPoint(Func<FlowContext, Task> onExecute)
        {
            this.onExecute = onExecute;
        }


        public Task Execute(FlowContext context)
        {
            return onExecute(context);
        }
    }
}
