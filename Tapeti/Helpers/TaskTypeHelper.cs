using System;
using System.Threading.Tasks;

namespace Tapeti.Helpers
{
    /// <summary>
    /// Determines if a type is a Task, ValueTask or other type.
    /// </summary>
    public enum TaskType
    {
        /// <summary>
        /// Type is not a Task or ValueTask.
        /// </summary>
        None,

        /// <summary>
        /// Type is a Task or Task&lt;T&gt;
        /// </summary>
        Task,

        /// <summary>
        /// Type is a ValueTask or ValueTask&lt;T&gt;
        /// </summary>
        ValueTask
    }


    /// <summary>
    /// Helper methods for working with synchronous and asynchronous versions of methods.
    /// </summary>
    public static class TaskTypeHelper
    {
        /// <summary>
        /// Determines if the given type matches the predicate, taking Task and ValueTask types into account.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="predicate"></param>
        /// <param name="taskType"></param>
        /// <param name="actualType"></param>
        public static bool IsTypeOrTaskOf(this Type type, Func<Type, bool> predicate, out TaskType taskType, out Type actualType)
        {
            if (type == typeof(Task))
            {
                taskType = TaskType.Task;
                actualType = type;
                return false;
            }

            if (type == typeof(ValueTask))
            {
                taskType = TaskType.ValueTask;
                actualType = type;
                return false;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                taskType = TaskType.Task;

                var genericArguments = type.GetGenericArguments();
                if (genericArguments.Length == 1 && predicate(genericArguments[0]))
                {
                    actualType = genericArguments[0];
                    return true;
                }
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                taskType = TaskType.ValueTask;

                var genericArguments = type.GetGenericArguments();
                if (genericArguments.Length == 1 && predicate(genericArguments[0]))
                {
                    actualType = genericArguments[0];
                    return true;
                }
            }

            taskType = TaskType.None;
            actualType = type;
            return predicate(type);
        }


        /// <summary>
        /// Determines if the given type matches the predicate, taking Task types into account.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="predicate"></param>
        /// <param name="taskType"></param>
        public static bool IsTypeOrTaskOf(this Type type, Func<Type, bool> predicate, out TaskType taskType)
        {
            return IsTypeOrTaskOf(type, predicate, out taskType, out _);
        }


        /// <summary>
        /// Determines if the given type matches the compareTo type, taking Task types into account.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="compareTo"></param>
        /// <param name="taskType"></param>
        public static bool IsTypeOrTaskOf(this Type type, Type compareTo, out TaskType taskType)
        {
            return IsTypeOrTaskOf(type, t => t == compareTo, out taskType);
        }
    }
}
