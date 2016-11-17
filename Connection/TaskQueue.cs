using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tapeti.Connection
{
    public class TaskQueue
    {
        private readonly object previousTaskLock = new object();
        private Task previousTask = Task.CompletedTask;


        public Task Add(Action action)
        {
            lock (previousTaskLock)
            {
                previousTask = previousTask.ContinueWith(t => action(), CancellationToken.None
                    , TaskContinuationOptions.None
                    , TaskScheduler.Default);
                return previousTask;
            }
        }
    }
}
