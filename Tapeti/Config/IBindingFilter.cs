using System.Threading.Tasks;

namespace Tapeti.Config
{
    public interface IBindingFilter
    {
        Task<bool> Accept(IMessageContext context, IBinding binding);
    }
}
