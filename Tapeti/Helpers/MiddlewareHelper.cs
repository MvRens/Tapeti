using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tapeti.Helpers
{
    public static class MiddlewareHelper
    {
        public static void Go<T>(IReadOnlyList<T> middleware, Action<T, Action> handle, Action lastHandler)
        {
            var handlerIndex = middleware?.Count - 1 ?? -1;
            if (middleware == null || handlerIndex == -1)
            {
                lastHandler();
                return;
            }

            Action handleNext = null;

            handleNext = () =>
            {
                handlerIndex--;
                if (handlerIndex >= 0)
                    handle(middleware[handlerIndex], handleNext);
                else
                    lastHandler();
            };

            handle(middleware[handlerIndex], handleNext);
        }


        public static async Task GoAsync<T>(IReadOnlyList<T> middleware, Func<T, Func<Task>, Task> handle, Func<Task> lastHandler)
        {
            var handlerIndex = middleware?.Count - 1 ?? -1;
            if (middleware == null || handlerIndex == -1)
            {
                await lastHandler();
                return;
            }

            Func<Task> handleNext = null;

            handleNext = async () =>
            {
                handlerIndex--;
                if (handlerIndex >= 0)
                    await handle(middleware[handlerIndex], handleNext);
                else
                    await lastHandler();
            };

            await handle(middleware[handlerIndex], handleNext);
        }
    }
}
