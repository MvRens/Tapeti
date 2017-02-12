using System.Threading.Tasks;

namespace Tapeti
{
    public interface ISubscriber
    {
        Task Resume();
    }
}
