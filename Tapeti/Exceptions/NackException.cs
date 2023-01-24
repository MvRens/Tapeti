using System;

namespace Tapeti.Exceptions
{
    /// <summary>
    /// Raised when a message is nacked by the message bus.
    /// </summary>
    public class NackException : Exception
    {
        /// <inheritdoc />
        public NackException(string message) : base(message) { }
    }
}
