using System;
using JetBrains.Annotations;

namespace Tapeti.Flow.Annotations
{
    [AttributeUsage(AttributeTargets.Method)]
    [MeansImplicitUse]
    public class StartAttribute : Attribute
    {
    }
}
