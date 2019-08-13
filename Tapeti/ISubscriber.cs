using System.Threading.Tasks;

namespace Tapeti
{
    /// <summary>
    /// Manages subscriptions to queues as configured by the bindings.
    /// </summary>
    public interface ISubscriber
    {
        /// <summary>
        /// Starts consuming from the subscribed queues if not already started.
        /// </summary>
        Task Resume();
    }
}
