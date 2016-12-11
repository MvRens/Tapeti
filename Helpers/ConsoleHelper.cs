using System;

namespace Tapeti.Helpers
{
    public static class ConsoleHelper
    {
        // Source: http://stackoverflow.com/questions/6408588/how-to-tell-if-there-is-a-console
        public static bool IsAvailable()
        {
            try
            {
                // ReSharper disable once UnusedVariable - that's why it's called dummy
                var dummy = Console.WindowHeight;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
