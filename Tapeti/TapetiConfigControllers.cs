﻿using System;
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

            foreach (var method in controller.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.MemberType == MemberTypes.Method && m.DeclaringType != typeof(object) && (m as MethodInfo)?.IsSpecialName == false)
                .Select(m => (MethodInfo)m))
            {
                // TODO create binding for method

                /*
                var context = new BindingContext(method);
                var messageHandler = GetMessageHandler(context, method);
                if (messageHandler == null)
                    continue;
                */

                var methodQueueInfo = GetQueueInfo(method) ?? controllerQueueInfo;
                if (methodQueueInfo == null || !methodQueueInfo.IsValid)
                    throw new TopologyConfigurationException(
                        $"Method {method.Name} or controller {controller.Name} requires a queue attribute");

                /*
                var handlerInfo = new Binding
                {
                    Controller = controller,
                    Method = method,
                    QueueInfo = methodQueueInfo,
                    QueueBindingMode = context.QueueBindingMode,
                    MessageClass = context.MessageClass,
                    MessageHandler = messageHandler,
                    MessageMiddleware = context.MessageMiddleware,
                    MessageFilterMiddleware = context.MessageFilterMiddleware
                };

                if (methodQueueInfo.Dynamic.GetValueOrDefault())
                    AddDynamicRegistration(handlerInfo);
                else
                    AddStaticRegistration(handlerInfo);
                */

                builder.RegisterBinding(new ControllerMethodBinding(controller, method, methodQueueInfo));
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
                return new ControllerMethodBinding.QueueInfo { Dynamic = true, Name = dynamicQueueAttribute.Prefix };

            return durableQueueAttribute != null 
                ? new ControllerMethodBinding.QueueInfo { Dynamic = false, Name = durableQueueAttribute.Name } 
                : null;
        }
    }
}
