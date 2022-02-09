using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tapeti.Helpers
{
    /// <summary>
    /// Helper class for executing the middleware pattern.
    /// </summary>
    public static class MiddlewareHelper
    {
        /// <summary>
        /// Executes the chain of middleware synchronously, starting with the last item in the list.
        /// </summary>
        /// <param name="middleware">The list of middleware to run</param>
        /// <param name="handle">Receives the middleware which should be called and a reference to the action which will call the next. Pass this on to the middleware.</param>
        /// <param name="lastHandler">The action to execute when the innermost middleware calls next.</param>
        /// <typeparam name="T"></typeparam>
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


        /// <summary>
        /// Executes the chain of middleware asynchronously, starting with the last item in the list.
        /// </summary>
        /// <param name="middleware">The list of middleware to run</param>
        /// <param name="handle">Receives the middleware which should be called and a reference to the action which will call the next. Pass this on to the middleware.</param>
        /// <param name="lastHandler">The action to execute when the innermost middleware calls next.</param>
        /// <typeparam name="T"></typeparam>
        public static async ValueTask GoAsync<T>(IReadOnlyList<T> middleware, Func<T, Func<ValueTask>, ValueTask> handle, Func<ValueTask> lastHandler)
        {
            var handlerIndex = middleware?.Count - 1 ?? -1;
            if (middleware == null || handlerIndex == -1)
            {
                await lastHandler();
                return;
            }

            async ValueTask HandleNext()
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
