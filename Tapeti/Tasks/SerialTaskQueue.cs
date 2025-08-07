using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Tapeti.Tasks
{
    /// <summary>
    /// An implementation of a queue which runs tasks serially.
    /// </summary>
    public class SerialTaskQueue : IAsyncDisposable
    {
        private readonly BlockingCollection<Func<ValueTask>> queue = new();
        private readonly CancellationTokenSource cancellationTokenSource = new();


        /// <inheritdoc cref="SerialTaskQueue"/>
        public SerialTaskQueue()
        {
            new Thread(Work).Start();
        }


        /// <summary>
        /// Add the specified method to the task queue.
        /// </summary>
        public ValueTask Add(Func<ValueTask> func)
        {
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
            var taskCompleted = new TaskCompletionSource();

            queue.Add(async () =>
            {
                if (cancellationTokenSource.IsCancellationRequested)
                    taskCompleted.TrySetCanceled();

                try
                {
                    await func();
                    taskCompleted.SetResult();
                }
                catch (Exception e)
                {
                    taskCompleted.SetException(e);
                }
            });

            return new ValueTask(taskCompleted.Task);
        }


        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            var queueEmpty = new TaskCompletionSource();
            queue.Add(() =>
            {
                queueEmpty.SetResult();
                return default;
            });

            queue.CompleteAdding();
            await cancellationTokenSource.CancelAsync();

            await queueEmpty.Task;
        }


        private void Work()
        {
            foreach (var taskFunc in queue.GetConsumingEnumerable(CancellationToken.None))
            {
                try
                {
                    var task = taskFunc();
                    if (!task.IsCompleted)
                        task.AsTask().GetAwaiter().GetResult();
                }
                catch
                {
                    // The task should not leak any exceptions, Add should have taken care of that.
                }
            }
        }
    }
}
