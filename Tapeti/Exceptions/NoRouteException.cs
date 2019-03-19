using System;

namespace Tapeti.Exceptions
{
    public class NoRouteException : Exception
    {
        public NoRouteException(string message) : base(message) { }
    }
}
