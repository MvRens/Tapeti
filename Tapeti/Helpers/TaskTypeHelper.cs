using System;
using System.Threading.Tasks;

namespace Tapeti.Helpers
{
    public static class TaskTypeHelper
    {
        public static bool IsTypeOrTaskOf(this Type type, Func<Type, bool> predicate, out bool isTaskOf, out Type actualType)
        {
            if (type == typeof(Task))
            {
                isTaskOf = false;
                actualType = type;
                return false;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                isTaskOf = true;

                var genericArguments = type.GetGenericArguments();
                if (genericArguments.Length == 1 && predicate(genericArguments[0]))
                {
                    actualType = genericArguments[0];
                    return true;
                }
            }

            isTaskOf = false;
            actualType = type;
            return predicate(type);
        }


        public static bool IsTypeOrTaskOf(this Type type, Func<Type, bool> predicate, out bool isTaskOf)
        {
            Type actualType;
            return IsTypeOrTaskOf(type, predicate, out isTaskOf, out actualType);
        }

        public static bool IsTypeOrTaskOf(this Type type, Type compareTo, out bool isTaskOf)
        {
            return IsTypeOrTaskOf(type, t => t == compareTo, out isTaskOf);
        }
    }
}
