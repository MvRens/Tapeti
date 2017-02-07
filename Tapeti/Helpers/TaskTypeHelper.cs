using System;
using System.Threading.Tasks;

namespace Tapeti.Helpers
{
    public static class TaskTypeHelper
    {
        public static bool IsTypeOrTaskOf(this Type type, Func<Type, bool> predicate, out bool isTask)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                isTask = true;

                var genericArguments = type.GetGenericArguments();
                return genericArguments.Length == 1 && predicate(genericArguments[0]);
            }

            isTask = false;
            return predicate(type);
        }


        public static bool IsTypeOrTaskOf(this Type type, Type compareTo, out bool isTask)
        {
            return IsTypeOrTaskOf(type, t => t == compareTo, out isTask);
        }
    }
}
