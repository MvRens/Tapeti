using System.Threading.Tasks;
using System;

namespace Tapeti.Tests.Helpers
{
    internal static class TaskExtensions
    {
        public static async Task WithTimeout(this Task task, TimeSpan timeout)
        {
            var timeoutTask = Task.Delay(timeout);
            if (await Task.WhenAny(task, timeoutTask) == timeoutTask)
                throw new TimeoutException("Task took too long to complete");
        }
    }
}
