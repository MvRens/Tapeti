using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tapeti.Tasks
{
    /// <summary>
    /// An implementation of a queue which runs tasks serially.
    /// </summary>
    public class SerialTaskQueue : IDisposable
    {
        private readonly object previousTaskLock = new();
        private Task previousTask = Task.CompletedTask;


        /// <summary>
        /// Add the specified synchronous action to the task queue.
        /// </summary>
        /// <param name="action"></param>
        public Task Add(Action action)
        {
            lock (previousTaskLock)
            {
                previousTask = previousTask.ContinueWith(
                    _ => action(), 
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);

                return previousTask;
            }
        }


        /// <summary>
        /// Add the specified asynchronous method to the task queue.
        /// </summary>
        /// <param name="func"></param>
        public Task Add(Func<Task> func)
        {
            lock (previousTaskLock)
            {
                var task = previousTask.ContinueWith(
                    _ => func(), 
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);

                previousTask = task;

                // 'task' completes at the moment a Task is returned (for example, an await is encountered),
                // this is used to chain the next. We return the unwrapped Task however, so that the caller
                // awaits until the full task chain has completed.
                return task.Unwrap();
            }
        }


        /// <inheritdoc />
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
