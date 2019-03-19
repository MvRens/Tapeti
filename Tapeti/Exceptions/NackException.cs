using System;

namespace Tapeti.Exceptions
{
    public class NackException : Exception
    {
        public NackException(string message) : base(message) { }
    }
}
