using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Tapeti.Flow.SQL
{
    internal class SqlRetryHelper
    {
        public static readonly TimeSpan[] ExponentialBackoff = {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(13),
            TimeSpan.FromSeconds(21),
            TimeSpan.FromSeconds(34),
            TimeSpan.FromSeconds(55)
        };


        public static async Task Execute(Func<Task> callback)
        {
            var retryAttempt = 0;

            while (true)
            {
                try
                {
                    await callback().ConfigureAwait(false);
                    break;
                }
                catch (SqlException e)
                {
                    if (SqlExceptionHelper.IsTransientError(e))
                    {
                        await Task.Delay(ExponentialBackoff[retryAttempt]).ConfigureAwait(false);
                        if (retryAttempt < ExponentialBackoff.Length - 1)
                            retryAttempt++;
                    }
                    else
                        throw;                
                }
            }
        }


        public static async Task<T> Execute<T>(Func<Task<T>> callback)
        {
            var returnValue = default(T);

            await Execute(async () =>
            {
                returnValue = await callback().ConfigureAwait(false);
            }).ConfigureAwait(false);

            return returnValue!;
        }
    }
}
