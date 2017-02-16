using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    internal interface IExecutableYieldPoint : IYieldPoint
    {
        bool StoreState { get; }
        Task Execute(FlowContext context);
    }
}
