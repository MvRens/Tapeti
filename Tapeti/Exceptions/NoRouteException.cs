using System;

namespace Tapeti.Exceptions
{
    /// <inheritdoc />
    /// <summary>
    /// Raised when a mandatory message has no route.
    /// </summary>
    public class NoRouteException : Exception
    {
        /// <inheritdoc />
        public NoRouteException(string message) : base(message) { }
    }
}
