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
        private readonly Task worker;


        /// <inheritdoc cref="SerialTaskQueue"/>
        public SerialTaskQueue()
        {
            worker = Task.Run(Work);
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

            await cancellationTokenSource.CancelAsync();
            queue.CompleteAdding();

            await worker;
        }


        private async Task Work()
        {
            try
            {
                foreach (var task in queue.GetConsumingEnumerable(cancellationTokenSource.Token))
                {
                    try
                    {
                        await task();
                    }
                    catch
                    {
                        // The task should not leak any exceptions, Add should have taken care of that.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                    throw;
            }
        }
    }
}
