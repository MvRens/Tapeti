using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    internal interface IStateYieldPoint : IYieldPoint
    {
        bool StoreState { get; }
    }


    internal interface IExecutableYieldPoint : IYieldPoint
    {
        Task Execute(FlowContext context);
    }
}
