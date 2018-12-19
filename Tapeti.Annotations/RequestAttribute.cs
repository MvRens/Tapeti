using System;

namespace Tapeti.Annotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RequestAttribute : Attribute
    {
        public Type Response { get; set; }
    }
}
