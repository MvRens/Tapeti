using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tapeti
{
    /// <summary>
    /// Tracks the number of currently running message handlers.
    /// </summary>
    public interface IMessageHandlerTracker
    {
        /// <summary>
        /// Registers the start of a message handler.
        /// </summary>
        void Enter();

        /// <summary>
        /// Signifies that the message handler task is still running but the consumer must finish
        /// (eg. due to a channel shutdown). The task will be monitored by the MessageHandlerTracker,
        /// </summary>
        /// <remarks>
        /// <see cref="Exit"/> must still be called afterwards.
        /// </remarks>
        void Detach(Task messageHandlerTask);

        /// <summary>
        /// Registers the end of a message handler.
        /// </summary>
        void Exit();

        /// <summary>
        /// Wait for all message handlers to finish running.
        /// </summary>
        /// <remarks>
        /// When using a non-infinite timeout, detached message handlers may still be running after this method
        /// throws a TimeoutException. Any subsequent calls to WaitAll will wait for these tasks again.
        /// </remarks>
        /// <param name="timeout">The timeout after which an OperationCanceledException is thrown.</param>
        /// <param name="cancellationToken"></param>
        ValueTask WaitAll(TimeSpan timeout, CancellationToken cancellationToken);
    }
}
