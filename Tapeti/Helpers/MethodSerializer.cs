using System.Reflection;
using System.Text.RegularExpressions;

namespace Tapeti.Helpers
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


        private static readonly Regex DeserializeRegex = new("^(?<method>.+?)@(?<assembly>.+?):(?<type>.+?)$");

        
        /// <summary>
        /// Deserializes the serialized method representation back into it's MethodInfo, or null if not found.
        /// </summary>
        /// <param name="serializedMethod"></param>
        public static MethodInfo? Deserialize(string serializedMethod)
        {
            var match = DeserializeRegex.Match(serializedMethod);
            if (!match.Success)
                return null;

            Assembly assembly;
            try
            {
                assembly = Assembly.Load(match.Groups["assembly"].Value);
            }
            catch
            {
                return null;
            }

            var declaringType = assembly.GetType(match.Groups["type"].Value);
            return declaringType?.GetMethod(match.Groups["method"].Value);
        }
    }
}
