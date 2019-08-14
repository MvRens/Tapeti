using System;
using System.Threading.Tasks;

namespace Tapeti.Helpers
{
    /// <summary>
    /// Helper methods for working with synchronous and asynchronous versions of methods.
    /// </summary>
    public static class TaskTypeHelper
    {
        /// <summary>
        /// Determines if the given type matches the predicate, taking Task types into account.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="predicate"></param>
        /// <param name="isTaskOf"></param>
        /// <param name="actualType"></param>
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


        /// <summary>
        /// Determines if the given type matches the predicate, taking Task types into account.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="predicate"></param>
        /// <param name="isTaskOf"></param>
        public static bool IsTypeOrTaskOf(this Type type, Func<Type, bool> predicate, out bool isTaskOf)
        {
            return IsTypeOrTaskOf(type, predicate, out isTaskOf, out _);
        }


        /// <summary>
        /// Determines if the given type matches the compareTo type, taking Task types into account.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="compareTo"></param>
        /// <param name="isTaskOf"></param>
        public static bool IsTypeOrTaskOf(this Type type, Type compareTo, out bool isTaskOf)
        {
            return IsTypeOrTaskOf(type, t => t == compareTo, out isTaskOf);
        }
    }
}
