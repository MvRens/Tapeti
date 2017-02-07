using System.Reflection;

namespace Tapeti.Flow.FlowHelpers
{
    public static class MethodSerializer
    {
        public static string Serialize(MethodInfo method)
        {
            return method.Name + '@' + method.DeclaringType?.Assembly.GetName().Name + ':' + method.DeclaringType?.FullName;
        }
    }
}
