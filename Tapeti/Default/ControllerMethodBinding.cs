using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Helpers;

namespace Tapeti.Default
{
    /// <inheritdoc />
    /// <summary>
    /// Binding implementation for controller methods. Do not instantiate this class yourself,
    /// instead use the ITapetiConfigBuilder RegisterController / RegisterAllControllers extension
    /// methods.
    /// </summary>
    internal class ControllerMethodBinding : IControllerMethodBinding
    {
        /// <summary>
        /// Contains all the required information to bind a controller method to a queue.
        /// </summary>
        public struct BindingInfo
        {
            /// <summary>
            /// The controller type associated with this binding. 
            /// </summary>
            public Type ControllerType;

            /// <summary>
            /// The method called when this binding is invoked.
            /// </summary>
            public MethodInfo Method;

            /// <summary>
            /// The queue this binding consumes.
            /// </summary>
            public QueueInfo QueueInfo;

            /// <summary>
            /// The message class handled by this binding's method.
            /// </summary>
            public Type MessageClass;

            /// <summary>
            /// Indicates whether this method accepts messages to the exchange by routing key, or direct-to-queue only.
            /// </summary>
            public BindingTargetMode BindingTargetMode;

            /// <summary>
            /// Indicates if the method or controller is marked with the Obsolete attribute, indicating it should
            /// only handle messages already in the queue and not bind to the routing key for new messages.
            /// </summary>
            public bool IsObsolete;

            /// <summary>
            /// Value factories for the method parameters.
            /// </summary>
            public IEnumerable<ValueFactory> ParameterFactories;

            /// <summary>
            /// The return value handler.
            /// </summary>
            public ResultHandler ResultHandler;


            /// <summary>
            /// Filter middleware as registered by the binding middleware.
            /// </summary>
            public IReadOnlyList<IControllerFilterMiddleware> FilterMiddleware;

            /// <summary>
            /// Message middleware as registered by the binding middleware.
            /// </summary>
            public IReadOnlyList<IControllerMessageMiddleware> MessageMiddleware;

            /// <summary>
            /// Cleanup middleware as registered by the binding middleware.
            /// </summary>
            public IReadOnlyList<IControllerCleanupMiddleware> CleanupMiddleware;
        }


        private readonly IDependencyResolver dependencyResolver;
        private readonly BindingInfo bindingInfo;

        private readonly MessageHandlerFunc messageHandler;


        /// <inheritdoc />
        public string QueueName { get; private set; }

        /// <inheritdoc />
        public QueueType QueueType => bindingInfo.QueueInfo.QueueType;

        /// <inheritdoc />
        public Type Controller => bindingInfo.ControllerType;

        /// <inheritdoc />
        public MethodInfo Method => bindingInfo.Method;


        /// <inheritdoc />
        public ControllerMethodBinding(IDependencyResolver dependencyResolver, BindingInfo bindingInfo)
        {
            this.dependencyResolver = dependencyResolver;
            this.bindingInfo = bindingInfo;

            messageHandler = WrapMethod(bindingInfo.Method, bindingInfo.ParameterFactories, bindingInfo.ResultHandler);
        }


        /// <inheritdoc />
        public async Task Apply(IBindingTarget target)
        {
            if (!bindingInfo.IsObsolete)
            {
                switch (bindingInfo.BindingTargetMode)
                {
                    case BindingTargetMode.Default:
                        if (bindingInfo.QueueInfo.QueueType == QueueType.Dynamic)
                            QueueName = await target.BindDynamic(bindingInfo.MessageClass, bindingInfo.QueueInfo.Name);
                        else
                        {
                            await target.BindDurable(bindingInfo.MessageClass, bindingInfo.QueueInfo.Name);
                            QueueName = bindingInfo.QueueInfo.Name;
                        }

                        break;

                    case BindingTargetMode.Direct:
                        if (bindingInfo.QueueInfo.QueueType == QueueType.Dynamic)
                            QueueName = await target.BindDynamicDirect(bindingInfo.MessageClass, bindingInfo.QueueInfo.Name);
                        else
                        {
                            await target.BindDurableDirect(bindingInfo.QueueInfo.Name);
                            QueueName = bindingInfo.QueueInfo.Name;
                        }

                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(bindingInfo.BindingTargetMode), bindingInfo.BindingTargetMode, "Invalid BindingTargetMode");
                }
            }
            else if (bindingInfo.QueueInfo.QueueType == QueueType.Durable)
            {
                await target.BindDurableObsolete(bindingInfo.QueueInfo.Name);
                QueueName = bindingInfo.QueueInfo.Name;
            }
        }


        /// <inheritdoc />
        public bool Accept(Type messageClass)
        {
            return messageClass == bindingInfo.MessageClass;
        }


        /// <inheritdoc />
        public async Task Invoke(IMessageContext context)
        {
            var controller = dependencyResolver.Resolve(bindingInfo.ControllerType);
            
            using (var controllerContext = new ControllerMessageContext(context)
            {
                Controller = controller
            })
            {
                if (!await FilterAllowed(controllerContext))
                    return;


                await MiddlewareHelper.GoAsync(
                    bindingInfo.MessageMiddleware,
                    async (handler, next) => await handler.Handle(controllerContext, next),
                    async () => await messageHandler(controllerContext));
            }
        }


        /// <inheritdoc />
        public async Task Cleanup(IMessageContext context, ConsumeResult consumeResult)
        {
            using (var controllerContext = new ControllerMessageContext(context)
            {
                Controller = null
            })
            {
                await MiddlewareHelper.GoAsync(
                    bindingInfo.CleanupMiddleware,
                    async (handler, next) => await handler.Cleanup(controllerContext, consumeResult, next),
                    () => Task.CompletedTask);
            }
        }


        private async Task<bool> FilterAllowed(IControllerMessageContext context)
        {
            var allowed = false;
            await MiddlewareHelper.GoAsync(
                bindingInfo.FilterMiddleware,
                async (handler, next) => await handler.Filter(context, next),
                () =>
                {
                    allowed = true;
                    return Task.CompletedTask;
                });

            return allowed;
        }


        private delegate Task MessageHandlerFunc(IControllerMessageContext context);


        private static MessageHandlerFunc WrapMethod(MethodInfo method, IEnumerable<ValueFactory> parameterFactories, ResultHandler resultHandler)
        {
            if (resultHandler != null)
                return WrapResultHandlerMethod(method, parameterFactories, resultHandler);

            if (method.ReturnType == typeof(void))
                return WrapNullMethod(method, parameterFactories);

            if (method.ReturnType == typeof(Task))
                return WrapTaskMethod(method, parameterFactories);

            if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                return WrapGenericTaskMethod(method, parameterFactories);

            return WrapObjectMethod(method, parameterFactories);
        }


        private static MessageHandlerFunc WrapResultHandlerMethod(MethodBase method, IEnumerable<ValueFactory> parameterFactories, ResultHandler resultHandler)
        {
            return context =>
            {
                var result = method.Invoke(context.Controller, parameterFactories.Select(p => p(context)).ToArray());
                return resultHandler(context, result);
            };
        }

        private static MessageHandlerFunc WrapNullMethod(MethodBase method, IEnumerable<ValueFactory> parameterFactories)
        {
            return context =>
            {
                method.Invoke(context.Controller, parameterFactories.Select(p => p(context)).ToArray());
                return Task.CompletedTask;
            };
        }


        private static MessageHandlerFunc WrapTaskMethod(MethodBase method, IEnumerable<ValueFactory> parameterFactories)
        {
            return context => (Task)method.Invoke(context.Controller, parameterFactories.Select(p => p(context)).ToArray());
        }


        private static MessageHandlerFunc WrapGenericTaskMethod(MethodBase method, IEnumerable<ValueFactory> parameterFactories)
        {
            return context =>
            {
                return (Task<object>)method.Invoke(context.Controller, parameterFactories.Select(p => p(context)).ToArray());
            };
        }


        private static MessageHandlerFunc WrapObjectMethod(MethodBase method, IEnumerable<ValueFactory> parameterFactories)
        {
            return context =>
            {
                return Task.FromResult(method.Invoke(context.Controller, parameterFactories.Select(p => p(context)).ToArray()));
            };
        }



        /// <summary>
        /// Contains information about the queue linked to the controller method.
        /// </summary>
        public class QueueInfo
        {
            /// <summary>
            /// The type of queue this binding consumes.
            /// </summary>
            public QueueType QueueType { get; set; }

            /// <summary>
            /// The name of the durable queue, or optional prefix of the dynamic queue.
            /// </summary>
            public string Name { get; set; }


            /// <summary>
            /// Determines if the QueueInfo properties contain a valid combination.
            /// </summary>
            public bool IsValid => QueueType == QueueType.Dynamic || !string.IsNullOrEmpty(Name);
        }
    }
}
