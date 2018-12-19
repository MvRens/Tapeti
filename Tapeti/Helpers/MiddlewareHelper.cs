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

            void HandleNext()
            {
                handlerIndex--;
                if (handlerIndex >= 0)
                    handle(middleware[handlerIndex], HandleNext);
                else
                    lastHandler();
            }

            handle(middleware[handlerIndex], HandleNext);
        }


        public static async Task GoAsync<T>(IReadOnlyList<T> middleware, Func<T, Func<Task>, Task> handle, Func<Task> lastHandler)
        {
            var handlerIndex = middleware?.Count - 1 ?? -1;
            if (middleware == null || handlerIndex == -1)
            {
                await lastHandler();
                return;
            }

            async Task HandleNext()
            {
                handlerIndex--;
                if (handlerIndex >= 0)
                    await handle(middleware[handlerIndex], HandleNext);
                else
                    await lastHandler();
            }

            await handle(middleware[handlerIndex], HandleNext);
        }
    }
}
