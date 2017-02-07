using System;

namespace Tapeti.Flow.Annotations
{
    public class RequestAttribute : Attribute
    {
        public Type Response { get; set; }
    }
}
