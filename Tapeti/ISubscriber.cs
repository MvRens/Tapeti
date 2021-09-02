using System;
using System.Threading.Tasks;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

namespace Tapeti
{
    /// <summary>
    /// Manages subscriptions to queues as configured by the bindings.
    /// </summary>
    public interface ISubscriber : IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// Starts consuming from the subscribed queues if not already started.
        /// </summary>
        Task Resume();

        /// <summary>
        /// Stops consuming from the subscribed queues.
        /// </summary>
        Task Stop();
    }
}
