using System.Threading.Tasks;

namespace Tapeti
{
    public interface IPublisher
    {
        Task Publish(object message);
    }
}
