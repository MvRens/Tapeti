using System;
using System.Collections.Concurrent;
using System.Linq;
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
        private readonly ConcurrentDictionary<Task, bool> detachedTasks = [];


        /// <inheritdoc />
        public void Enter()
        {
            if (Interlocked.Increment(ref runningCount) == 1)
                idleEvent.Reset();
        }


        /// <inheritdoc />
        public void Detach(Task messageHandlerTask)
        {
            detachedTasks.TryAdd(messageHandlerTask, true);
            messageHandlerTask.ContinueWith(t =>
            {
                detachedTasks.TryRemove(t, out _);
            });
        }


        /// <inheritdoc />
        public void Exit()
        {
            if (Interlocked.Decrement(ref runningCount) == 0)
                idleEvent.Set();
        }


        /// <inheritdoc />
        public async ValueTask WaitAll(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var capturedDetachedTasks = detachedTasks.Keys
                .Append(idleEvent.WaitHandle.WaitOneAsync(CancellationToken.None, timeout))
                .ToArray();

            var timeoutTask = Task.Delay(timeout, cancellationToken);
            if (await Task.WhenAny(Task.WhenAll(capturedDetachedTasks), timeoutTask) != timeoutTask)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Message handlers did not complete within the specified timeout");
        }
    }
}
