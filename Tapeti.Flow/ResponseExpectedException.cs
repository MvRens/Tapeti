using System;

namespace Tapeti.Flow
{
    /// <summary>
    /// Raised when a response is expected to end a flow, but none was provided.
    /// </summary>
    public class ResponseExpectedException : Exception
    {
        /// <inheritdoc />
        public ResponseExpectedException(string message) : base(message) { }
    }
}
