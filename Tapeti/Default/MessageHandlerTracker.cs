using System;
using System.Threading;
using System.Threading.Tasks;
using Tapeti.Helpers;

namespace Tapeti.Default
{
    /// <inheritdoc />
    public class MessageHandlerTracker : IMessageHandlerTracker
    {
        private volatile int runningCount;
        private readonly ManualResetEventSlim idleEvent = new(true);


        /// <inheritdoc />
        public void Enter()
        {
            if (Interlocked.Increment(ref runningCount) == 1)
                idleEvent.Reset();
        }


        /// <inheritdoc />
        public void Exit()
        {
            if (Interlocked.Decrement(ref runningCount) == 0)
                idleEvent.Set();
        }


        /// <summary>
        /// Waits for the amount of currently running message handlers to reach zero.
        /// </summary>
        /// <param name="timeoutMilliseconds">The timeout after which an OperationCanceledException is thrown.</param>
        public Task WaitForIdle(int timeoutMilliseconds)
        {
            return idleEvent.WaitHandle.WaitOneAsync(CancellationToken.None, timeoutMilliseconds);
        }
    }
}
