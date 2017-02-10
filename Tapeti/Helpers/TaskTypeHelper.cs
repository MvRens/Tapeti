using System;
using System.Threading.Tasks;

namespace Tapeti.Helpers
{
    public static class TaskTypeHelper
    {
        public static bool IsTypeOrTaskOf(this Type type, Func<Type, bool> predicate, out bool isTask, out Type actualType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                isTask = true;

                var genericArguments = type.GetGenericArguments();
                if (genericArguments.Length == 1 && predicate(genericArguments[0]))
                {
                    actualType = genericArguments[0];
                    return true;
                }
            }

            isTask = false;
            actualType = type;
            return predicate(type);
        }


        public static bool IsTypeOrTaskOf(this Type type, Func<Type, bool> predicate, out bool isTask)
        {
            Type actualType;
            return IsTypeOrTaskOf(type, predicate, out isTask, out actualType);
        }

        public static bool IsTypeOrTaskOf(this Type type, Type compareTo, out bool isTask)
        {
            return IsTypeOrTaskOf(type, t => t == compareTo, out isTask);
        }
    }
}
