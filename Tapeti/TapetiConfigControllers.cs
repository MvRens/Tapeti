using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Tapeti.Config.Annotations;
using Tapeti.Config;
using Tapeti.Connection;
using Tapeti.Default;

// ReSharper disable UnusedMember.Global

namespace Tapeti
{
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

            var controllerQueueInfo = GetQueueInfo(controller, null);
            (builderAccess.DependencyResolver as IDependencyContainer)?.RegisterController(controller);

            var controllerIsObsolete = controller.GetCustomAttribute<ObsoleteAttribute>() != null;


            foreach (var method in controller.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.MemberType == MemberTypes.Method && m.DeclaringType != typeof(object) && (m as MethodInfo)?.IsSpecialName == false)
                .Select(m => (MethodInfo)m))
            {
                if (method.GetCustomAttributes<NoBindingAttribute>().Any())
                    continue;

                var methodIsObsolete = controllerIsObsolete || method.GetCustomAttribute<ObsoleteAttribute>() != null;

                var context = new ControllerBindingContext(controller, method, method.GetParameters(), method.ReturnParameter);

                if (method.GetResponseHandlerAttribute() != null)
                {
                    context.SetBindingTargetMode(BindingTargetMode.Direct);
                    context.Use(new ResponseFilterMiddleware());
                }


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

                var methodQueueInfo = GetQueueInfo(method, controllerQueueInfo);
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


        /// <inheritdoc cref="RegisterController"/>
        public static ITapetiConfigBuilder RegisterController<TController>(this ITapetiConfigBuilder builder) where TController : class
        {
            return RegisterController(builder, typeof(TController));
        }



        /// <summary>
        /// Registers all controllers in the specified assembly which are marked with the MessageController attribute.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="assembly">The assembly to scan for controllers.</param>
        public static ITapetiConfigBuilder RegisterAllControllers(this ITapetiConfigBuilder builder, Assembly assembly)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.HasMessageControllerAttribute()))
                RegisterController(builder, type);

            return builder;
        }


        /// <summary>
        /// Registers all controllers in the entry assembly which are marked with the MessageController attribute.
        /// </summary>
        /// <param name="builder"></param>
        public static ITapetiConfigBuilder RegisterAllControllers(this ITapetiConfigBuilder builder)
        {
            var assembly = Assembly.GetEntryAssembly();
            if (assembly == null)
                throw new InvalidOperationException("No EntryAssembly");

            return RegisterAllControllers(builder, assembly);
        }


        private static ControllerMethodBinding.QueueInfo? GetQueueInfo(MemberInfo member, ControllerMethodBinding.QueueInfo? fallbackQueueInfo)
        {
            var dynamicQueueAttribute = member.GetDynamicQueueAttribute();
            var durableQueueAttribute = member.GetDurableQueueAttribute();
            var queueArgumentsAttribute = member.GetQueueArgumentsAttribute();

            if (dynamicQueueAttribute != null && durableQueueAttribute != null)
                throw new TopologyConfigurationException($"Cannot combine static and dynamic queue attributes on controller {member.DeclaringType?.Name} method {member.Name}");

            if (dynamicQueueAttribute == null && durableQueueAttribute == null && (queueArgumentsAttribute == null || fallbackQueueInfo == null))
                return fallbackQueueInfo;


            QueueType queueType;
            string? name;
            

            if (dynamicQueueAttribute != null)
            {
                queueType = QueueType.Dynamic;
                name = dynamicQueueAttribute.Prefix;
            }
            else if (durableQueueAttribute != null)
            {
                queueType = QueueType.Durable;
                name = durableQueueAttribute.Name;
            }
            else
            {
                queueType = fallbackQueueInfo!.QueueType;
                name = fallbackQueueInfo.Name;
            }

            return new ControllerMethodBinding.QueueInfo(queueType, name)
            {
                QueueArguments = GetQueueArguments(queueArgumentsAttribute) ?? fallbackQueueInfo?.QueueArguments
            };
        }


        private static IRabbitMQArguments? GetQueueArguments(QueueArgumentsAttribute? queueArgumentsAttribute)
        {
            if (queueArgumentsAttribute == null)
                return null;

            var arguments = new RabbitMQArguments(queueArgumentsAttribute.CustomArguments.ToDictionary(
                    p => p.Key, 
                    p => p.Value switch
                    {
                        string stringValue => Encoding.UTF8.GetBytes(stringValue),
                        _ => p.Value
                    }
                ));

            if (queueArgumentsAttribute.MaxLength > 0)
                arguments.Add(@"x-max-length", queueArgumentsAttribute.MaxLength);

            if (queueArgumentsAttribute.MaxLengthBytes > 0)
                arguments.Add(@"x-max-length-bytes", queueArgumentsAttribute.MaxLengthBytes);

            if (queueArgumentsAttribute.MessageTTL > 0)
                arguments.Add(@"x-message-ttl", queueArgumentsAttribute.MessageTTL);

            switch (queueArgumentsAttribute.Overflow)
            {
                case RabbitMQOverflow.NotSpecified:
                    break;
                case RabbitMQOverflow.DropHead:
                    arguments.AddUTF8(@"x-overflow", @"drop-head");
                    break;
                case RabbitMQOverflow.RejectPublish:
                    arguments.AddUTF8(@"x-overflow", @"reject-publish");
                    break;
                case RabbitMQOverflow.RejectPublishDeadletter:
                    arguments.AddUTF8(@"x-overflow", @"reject-publish-dlx");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(queueArgumentsAttribute.Overflow), queueArgumentsAttribute.Overflow, "Unsupported Overflow value");
            }


            return arguments.Count > 0 ? arguments : null;
        }
    }
}
