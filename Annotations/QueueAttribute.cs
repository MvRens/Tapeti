using System;

namespace Tapeti.Annotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class QueueAttribute : Attribute
    {
        public string Name { get; set; }
        public bool Dynamic { get; set; }


        public QueueAttribute(string name = null)
        {
            Name = name;
            Dynamic = (name == null);
        }
    }
}
