using System;

namespace Tapeti.Flow.Annotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RequestAttribute : Attribute
    {
        public Type Response { get; set; }
    }
}
