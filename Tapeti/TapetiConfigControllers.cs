using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tapeti.Annotations;
using Tapeti.Config;
using Tapeti.Default;

// ReSharper disable UnusedMember.Global

namespace Tapeti
{
    /// <inheritdoc />
    /// <summary>
    /// Thrown when an issue is detected in a controller configuration.
    /// </summary>
    public class TopologyConfigurationException : Exception
    {
        /// <inheritdoc />
        public TopologyConfigurationException(string message) : base(message) { }
    }


    /// <summary>
    /// Extension methods for registering message controllers.
    /// </summary>
    public static class TapetiConfigControllers
    {
        /// <summary>
        /// Registers all public methods in the specified controller class as message handlers.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="controller">The controller class to register. The class and/or methods must be annotated with either the DurableQueue or DynamicQueue attribute.</param>
        public static ITapetiConfigBuilder RegisterController(this ITapetiConfigBuilder builder, Type controller)
        {
            var builderAccess = (ITapetiConfigBuilderAccess)builder;

            if (!controller.IsClass)
                throw new ArgumentException($"Controller {controller.Name} must be a class");

            var controllerQueueInfo = GetQueueInfo(controller);
            (builderAccess.DependencyResolver as IDependencyContainer)?.RegisterController(controller);

            var controllerIsObsolete = controller.GetCustomAttribute<ObsoleteAttribute>() != null;


            foreach (var method in controller.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.MemberType == MemberTypes.Method && m.DeclaringType != typeof(object) && (m as MethodInfo)?.IsSpecialName == false)
                .Select(m => (MethodInfo)m))
            {
                var methodIsObsolete = controllerIsObsolete || method.GetCustomAttribute<ObsoleteAttribute>() != null;

                var context = new ControllerBindingContext(method.GetParameters(), method.ReturnParameter)
                {
                    Controller = controller,
                    Method = method
                };


                if (method.GetCustomAttribute<ResponseHandlerAttribute>() != null)
                    context.SetBindingTargetMode(BindingTargetMode.Direct);


                var allowBinding = false;
                builderAccess.ApplyBindingMiddleware(context, () => { allowBinding = true; });

                if (!allowBinding)
                    continue;


                if (context.MessageClass == null)
                    throw new TopologyConfigurationException($"Method {method.Name} in controller {controller.Name} does not resolve to a message class");


                var invalidBindings = context.Parameters.Where(p => !p.HasBinding).ToList();
                if (invalidBindings.Count > 0)
                {
                    var parameterNames = string.Join(", ", invalidBindings.Select(p => p.Info.Name));
                    throw new TopologyConfigurationException($"Method {method.Name} in controller {method.DeclaringType?.Name} has unknown parameters: {parameterNames}");
                }

                var methodQueueInfo = GetQueueInfo(method) ?? controllerQueueInfo;
                if (methodQueueInfo is not { IsValid: true })
                    throw new TopologyConfigurationException(
                        $"Method {method.Name} or controller {controller.Name} requires a queue attribute");

                builder.RegisterBinding(new ControllerMethodBinding(builderAccess.DependencyResolver, new ControllerMethodBinding.BindingInfo
                {
                    ControllerType = controller,
                    Method = method,
                    QueueInfo = methodQueueInfo,
                    MessageClass = context.MessageClass,
                    BindingTargetMode = context.BindingTargetMode,
                    IsObsolete = methodIsObsolete,
                    ParameterFactories = context.GetParameterHandlers(),
                    ResultHandler = context.GetResultHandler(),

                    FilterMiddleware = context.Middleware.Where(m => m is IControllerFilterMiddleware).Cast<IControllerFilterMiddleware>().ToList(),
                    MessageMiddleware = context.Middleware.Where(m => m is IControllerMessageMiddleware).Cast<IControllerMessageMiddleware>().ToList(),
                    CleanupMiddleware = context.Middleware.Where(m => m is IControllerCleanupMiddleware).Cast<IControllerCleanupMiddleware>().ToList()
                }));
            }

            return builder;
        }


        /// <summary>
        /// Registers all controllers in the specified assembly which are marked with the MessageController attribute.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="assembly">The assembly to scan for controllers.</param>
        public static ITapetiConfigBuilder RegisterAllControllers(this ITapetiConfigBuilder builder, Assembly assembly)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsDefined(typeof(MessageControllerAttribute))))
                RegisterController(builder, type);

            return builder;
        }


        /// <summary>
        /// Registers all controllers in the entry assembly which are marked with the MessageController attribute.
        /// </summary>
        /// <param name="builder"></param>
        public static ITapetiConfigBuilder RegisterAllControllers(this ITapetiConfigBuilder builder)
        {
            return RegisterAllControllers(builder, Assembly.GetEntryAssembly());
        }


        private static ControllerMethodBinding.QueueInfo GetQueueInfo(MemberInfo member)
        {
            var dynamicQueueAttribute = member.GetCustomAttribute<DynamicQueueAttribute>();
            var durableQueueAttribute = member.GetCustomAttribute<DurableQueueAttribute>();

            if (dynamicQueueAttribute != null && durableQueueAttribute != null)
                throw new TopologyConfigurationException($"Cannot combine static and dynamic queue attributes on controller {member.DeclaringType?.Name} method {member.Name}");


            var queueArgumentsAttribute = member.GetCustomAttribute<QueueArgumentsAttribute>();

            if (dynamicQueueAttribute != null)
                return new ControllerMethodBinding.QueueInfo { QueueType = QueueType.Dynamic, Name = dynamicQueueAttribute.Prefix, QueueArguments = GetQueueArguments(queueArgumentsAttribute) };

            return durableQueueAttribute != null 
                ? new ControllerMethodBinding.QueueInfo { QueueType = QueueType.Durable, Name = durableQueueAttribute.Name, QueueArguments = GetQueueArguments(queueArgumentsAttribute) } 
                : null;
        }


        private static IReadOnlyDictionary<string, string> GetQueueArguments(QueueArgumentsAttribute queueArgumentsAttribute)
        {
            if (queueArgumentsAttribute == null)
                return null;

            #if NETSTANDARD2_1_OR_GREATER
            var arguments = new Dictionary<string, string>(queueArgumentsAttribute.CustomArguments);
            #else
            var arguments = new Dictionary<string, string>();
            foreach (var pair in queueArgumentsAttribute.CustomArguments)
                arguments.Add(pair.Key, pair.Value);
            #endif

            if (queueArgumentsAttribute.MaxLength > 0)
                arguments.Add(@"x-max-length", queueArgumentsAttribute.MaxLength.ToString());

            if (queueArgumentsAttribute.MaxLengthBytes > 0)
                arguments.Add(@"x-max-length-bytes", queueArgumentsAttribute.MaxLengthBytes.ToString());

            if (queueArgumentsAttribute.MessageTTL > 0)
                arguments.Add(@"x-message-ttl", queueArgumentsAttribute.MessageTTL.ToString());

            switch (queueArgumentsAttribute.Overflow)
            {
                case RabbitMQOverflow.NotSpecified:
                    break;
                case RabbitMQOverflow.DropHead:
                    arguments.Add(@"x-overflow", @"drop-head");
                    break;
                case RabbitMQOverflow.RejectPublish:
                    arguments.Add(@"x-overflow", @"reject-publish");
                    break;
                case RabbitMQOverflow.RejectPublishDeadletter:
                    arguments.Add(@"x-overflow", @"reject-publish-dlx");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(queueArgumentsAttribute.Overflow), queueArgumentsAttribute.Overflow, "Unsupported Overflow value");
            }


            return arguments.Count > 0 ? arguments : null;
        }
    }
}
