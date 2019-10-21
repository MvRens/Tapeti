using System.Reflection;

namespace Tapeti.Flow.FlowHelpers
{
    /// <summary>
    /// Converts a method into a unique string representation.
    /// </summary>
    public static class MethodSerializer
    {
        /// <summary>
        /// Converts a method into a unique string representation.
        /// </summary>
        /// <param name="method"></param>
        public static string Serialize(MethodInfo method)
        {
            return method.Name + '@' + method.DeclaringType?.Assembly.GetName().Name + ':' + method.DeclaringType?.FullName;
        }
    }
}
