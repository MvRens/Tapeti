using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tapeti.Tasks
{
    public class SingleThreadTaskQueue : IDisposable
    {
        private readonly object previousTaskLock = new object();
        private Task previousTask = Task.CompletedTask;

        private readonly Lazy<SingleThreadTaskScheduler> singleThreadScheduler = new Lazy<SingleThreadTaskScheduler>();


        public Task Add(Action action)
        {
            lock (previousTaskLock)
            {
                previousTask = previousTask.ContinueWith(t => action(), CancellationToken.None
                    , TaskContinuationOptions.None
                    , singleThreadScheduler.Value);

                return previousTask;
            }
        }


        public Task Add(Func<Task> func)
        {
            lock (previousTaskLock)
            {
                var task = previousTask.ContinueWith(t => func(), CancellationToken.None
                    , TaskContinuationOptions.None
                    , singleThreadScheduler.Value);

                previousTask = task;

                // 'task' completes at the moment a Task is returned (for example, an await is encountered),
                // this is used to chain the next. We return the unwrapped Task however, so that the caller
                // awaits until the full task chain has completed.
                return task.Unwrap();
            }
        }


        public void Dispose()
        {
            if (singleThreadScheduler.IsValueCreated)
                singleThreadScheduler.Value.Dispose();
        }
    }


    public class SingleThreadTaskScheduler : TaskScheduler, IDisposable
    {
        public override int MaximumConcurrencyLevel => 1;


        private readonly Queue<Task> scheduledTasks = new Queue<Task>();
        private bool disposed;


        public SingleThreadTaskScheduler()
        {
            // ReSharper disable once ObjectCreationAsStatement - fire and forget!
            new Thread(WorkerThread).Start();
        }


        public void Dispose()
        {
            lock (scheduledTasks)
            {
                disposed = true;
                Monitor.PulseAll(scheduledTasks);
            }
        }


        protected override void QueueTask(Task task)
        {
            if (disposed) return;

            lock (scheduledTasks)
            {
                scheduledTasks.Enqueue(task);
                Monitor.Pulse(scheduledTasks);
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }


        protected override IEnumerable<Task> GetScheduledTasks()
        {
            lock (scheduledTasks)
            {
                return scheduledTasks.ToList();
            }
        }


        private void WorkerThread()
        {
            while(true)
            {
                Task task;
                lock (scheduledTasks)
                {
                    task = WaitAndDequeueTask();
                }

                if (task == null)
                    break;

                TryExecuteTask(task);
            }
        }

        private Task WaitAndDequeueTask()
        {
            while (!scheduledTasks.Any() && !disposed)
                Monitor.Wait(scheduledTasks);

            return disposed ? null : scheduledTasks.Dequeue();
        }
    }
}
