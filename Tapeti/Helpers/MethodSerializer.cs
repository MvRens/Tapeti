using System.Reflection;
using System.Text.RegularExpressions;

namespace Tapeti.Helpers
{
    /// <summary>
    /// Converts a method into a unique string representation.
    /// </summary>
    /// <remarks>
    /// The format used is &lt;method&gt;@&lt;assembly&gt;:&lt;class&gt;
    /// <br/><br/>
    /// Example: <code>HandleQuoteResponse@FlowRequestResponse:FlowRequestResponse.SimpleFlowController</code>
    /// </remarks>
    public static class MethodSerializer
    {
        /// <inheritdoc cref="MethodSerializer"/>
        /// <param name="method"></param>
        public static string Serialize(MethodInfo method)
        {
            return method.Name + '@' + method.DeclaringType?.Assembly.GetName().Name + ':' + method.DeclaringType?.FullName;
        }


        private static readonly Regex DeserializeRegex = new("^(?<method>.+?)@(?<assembly>.+?):(?<type>.+?)$");


        /// <summary>
        /// Deconstructs the serialized method representation back into it's parts.
        /// </summary>
        public static bool TryDeconstruct(string serializedMethod, out string assemblyName, out string declaringTypeName, out string methodName)
        {
            var match = DeserializeRegex.Match(serializedMethod);
            if (!match.Success)
            {
                assemblyName = string.Empty;
                declaringTypeName = string.Empty;
                methodName = string.Empty;
                return false;
            }

            assemblyName = match.Groups["assembly"].Value;
            declaringTypeName = match.Groups["type"].Value;
            methodName = match.Groups["method"].Value;

            return true;
        }


        /// <summary>
        /// Deserializes the serialized method representation back into it's MethodInfo, or null if not found.
        /// </summary>
        /// <param name="serializedMethod"></param>
        public static MethodInfo? Deserialize(string serializedMethod)
        {
            if (!TryDeconstruct(serializedMethod, out var assemblyName, out var declaringTypeName, out var methodName))
                return null;

            Assembly assembly;
            try
            {
                assembly = Assembly.Load(assemblyName);
            }
            catch
            {
                return null;
            }

            var declaringType = assembly.GetType(declaringTypeName);
            return declaringType?.GetMethod(methodName);
        }
    }
}
