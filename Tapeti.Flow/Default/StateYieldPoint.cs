using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    internal class StateYieldPoint : IExecutableYieldPoint
    {
        public bool StoreState { get; }


        public StateYieldPoint(bool storeState)
        {
            StoreState = storeState;
        }


        public async Task Execute(FlowContext context)
        {
            if (StoreState)
                await context.EnsureStored();
        }
    }
}
