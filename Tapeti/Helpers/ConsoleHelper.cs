using System;

namespace Tapeti.Helpers
{
    /// <summary>
    /// Helper class for console applications.
    /// </summary>
    public static class ConsoleHelper
    {
        /// <summary>
        /// Determines if the application is running in a console.
        /// </summary>
        /// <remarks>
        /// Source: http://stackoverflow.com/questions/6408588/how-to-tell-if-there-is-a-console
        /// </remarks>
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
