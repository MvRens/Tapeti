using System;

namespace Tapeti.Flow
{
    public class YieldPointException : Exception
    {
        public YieldPointException(string message) : base(message) { }
        public YieldPointException(string message, Exception innerException) : base(message, innerException) { }
    }
}
