using System;
using JetBrains.Annotations;

namespace Tapeti.Flow.Annotations
{
    /// <summary>
    /// Marks this method as the start of a Tapeti Flow. Use IFlowStarter.Start to begin a new flow and
    /// call this method. Must return an IYieldPoint.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    [MeansImplicitUse]
    public class StartAttribute : Attribute
    {
    }
}
