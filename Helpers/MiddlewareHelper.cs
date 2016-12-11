using System;
using System.Collections.Generic;

namespace Tapeti.Helpers
{
    public static class MiddlewareHelper
    {
        public static void Go<T>(IReadOnlyList<T> middleware, Action<T, Action> handle)
        {
            var handlerIndex = middleware.Count - 1;
            if (handlerIndex == -1)
                return;

            Action handleNext = null;

            handleNext = () =>
            {
                handlerIndex--;
                if (handlerIndex >= 0)
                    handle(middleware[handlerIndex], handleNext);
            };

            handle(middleware[handlerIndex], handleNext);
        }
    }
}
