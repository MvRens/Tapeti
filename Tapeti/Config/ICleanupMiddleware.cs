using System.Threading.Tasks;

namespace Tapeti.Config
{
    public interface ICleanupMiddleware
    {
        Task Handle(IMessageContext context, HandlingResult handlingResult);
    }
}
