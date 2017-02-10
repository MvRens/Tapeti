using System;

namespace Tapeti.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ExchangeAttribute : Attribute
    {
        public string Name { get; set; }

        public ExchangeAttribute(string name)
        {
            Name = name;
        }
    }
}
