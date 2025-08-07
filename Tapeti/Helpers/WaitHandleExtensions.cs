using System.Threading.Tasks;
using System.Threading;
using System;

namespace Tapeti.Helpers
{
    /// <summary>
    /// Provides a WaitOneAsync method for <see cref="WaitHandle"/>.
    /// </summary>
    public static class WaitHandleExtensions
    {
        /// <summary>
        /// Provides a way to wait for a WaitHandle asynchronously.
        /// </summary>
        /// <remarks>
        /// Credit: <see href="https://stackoverflow.com/a/68632819"/>
        /// </remarks>
        public static Task WaitOneAsync(this WaitHandle waitHandle, CancellationToken cancellationToken, TimeSpan? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(waitHandle);

            var tcs = new TaskCompletionSource<bool>();
            var ctr = cancellationToken.Register(() => tcs.TrySetCanceled());

            var rwh = ThreadPool.RegisterWaitForSingleObject(waitHandle,
                (_, timedOut) =>
                {
                    if (timedOut)
                    {
                        tcs.TrySetCanceled();
                    }
                    else
                    {
                        tcs.TrySetResult(true);
                    }
                },
                null, timeout ?? Timeout.InfiniteTimeSpan, true);

            var task = tcs.Task;

            _ = task.ContinueWith(_ =>
            {
                rwh.Unregister(null);
                return ctr.Unregister();
            }, CancellationToken.None);

            return task;
        }
    }
}
