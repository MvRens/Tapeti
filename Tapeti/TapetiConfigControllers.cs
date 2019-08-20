using System;
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


            foreach (var method in controller.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.MemberType == MemberTypes.Method && m.DeclaringType != typeof(object) && (m as MethodInfo)?.IsSpecialName == false)
                .Select(m => (MethodInfo)m))
            {
                var methodQueueInfo = GetQueueInfo(method) ?? controllerQueueInfo;
                if (methodQueueInfo == null || !methodQueueInfo.IsValid)
                    throw new TopologyConfigurationException(
                        $"Method {method.Name} or controller {controller.Name} requires a queue attribute");


                var methodIsObsolete = controllerIsObsolete || method.GetCustomAttribute<ObsoleteAttribute>() != null;


                var context = new ControllerBindingContext(method.GetParameters(), method.ReturnParameter)
                {
                    Controller = controller,
                    Method = method
                };


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

            if (dynamicQueueAttribute != null)
                return new ControllerMethodBinding.QueueInfo { QueueType = QueueType.Dynamic, Name = dynamicQueueAttribute.Prefix };

            return durableQueueAttribute != null 
                ? new ControllerMethodBinding.QueueInfo { QueueType = QueueType.Durable, Name = durableQueueAttribute.Name } 
                : null;
        }
    }
}
