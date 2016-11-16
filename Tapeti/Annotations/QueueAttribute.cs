using System;

namespace Tapeti.Annotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class QueueAttribute : Attribute
    {
        public string Name { get; set; } = null;
        public bool Dynamic { get; set; } = false;
    }
}
