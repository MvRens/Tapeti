using System;

namespace Tapeti.Flow
{
    public class ResponseExpectedException : Exception
    {
        public ResponseExpectedException(string message) : base(message) { }
    }
}
