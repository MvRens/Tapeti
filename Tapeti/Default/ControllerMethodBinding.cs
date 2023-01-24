using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Connection;
using Tapeti.Helpers;

namespace Tapeti.Default
{
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
            public ResultHandler? ResultHandler;


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
        public string? QueueName { get; private set; }

        /// <inheritdoc />
        public QueueType? QueueType => bindingInfo.QueueInfo.QueueType;

        /// <inheritdoc />
        public Type Controller => bindingInfo.ControllerType;

        /// <inheritdoc />
        public MethodInfo Method => bindingInfo.Method;


        public ControllerMethodBinding(IDependencyResolver dependencyResolver, BindingInfo bindingInfo)
        {
            this.dependencyResolver = dependencyResolver;
            this.bindingInfo = bindingInfo;

            messageHandler = WrapMethod(bindingInfo.Method, bindingInfo.ParameterFactories, bindingInfo.ResultHandler);
        }


        /// <inheritdoc />
        public async ValueTask Apply(IBindingTarget target)
        {
            if (!bindingInfo.IsObsolete)
            {
                switch (bindingInfo.BindingTargetMode)
                {
                    case BindingTargetMode.Default:
                        if (bindingInfo.QueueInfo.QueueType == Config.QueueType.Dynamic)
                            QueueName = await target.BindDynamic(bindingInfo.MessageClass, bindingInfo.QueueInfo.Name, bindingInfo.QueueInfo.QueueArguments);
                        else
                        {
                            await target.BindDurable(bindingInfo.MessageClass, bindingInfo.QueueInfo.Name, bindingInfo.QueueInfo.QueueArguments);
                            QueueName = bindingInfo.QueueInfo.Name;
                        }

                        break;

                    case BindingTargetMode.Direct:
                        if (bindingInfo.QueueInfo.QueueType == Config.QueueType.Dynamic)
                            QueueName = await target.BindDynamicDirect(bindingInfo.MessageClass, bindingInfo.QueueInfo.Name, bindingInfo.QueueInfo.QueueArguments);
                        else
                        {
                            await target.BindDurableDirect(bindingInfo.QueueInfo.Name, bindingInfo.QueueInfo.QueueArguments);
                            QueueName = bindingInfo.QueueInfo.Name;
                        }

                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(bindingInfo.BindingTargetMode), bindingInfo.BindingTargetMode, "Invalid BindingTargetMode");
                }
            }
            else if (bindingInfo.QueueInfo.QueueType == Config.QueueType.Durable)
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
        public async ValueTask Invoke(IMessageContext context)
        {
            if (context.Binding == null)
                throw new InvalidOperationException("Invoke should not be called on a context without a binding");

            var controller = Method.IsStatic ? null : dependencyResolver.Resolve(bindingInfo.ControllerType);
            context.Store(new ControllerMessageContextPayload(controller, (IControllerMethodBinding)context.Binding));
            
            if (!await FilterAllowed(context))
                return;


            await MiddlewareHelper.GoAsync(
                bindingInfo.MessageMiddleware,
                async (handler, next) => await handler.Handle(context, next),
                async () => await messageHandler(context));
        }


        /// <inheritdoc />
        public async ValueTask Cleanup(IMessageContext context, ConsumeResult consumeResult)
        {
            await MiddlewareHelper.GoAsync(
                bindingInfo.CleanupMiddleware,
                async (handler, next) => await handler.Cleanup(context, consumeResult, next),
                () => default);
        }


        private async Task<bool> FilterAllowed(IMessageContext context)
        {
            var allowed = false;
            await MiddlewareHelper.GoAsync(
                bindingInfo.FilterMiddleware,
                async (handler, next) => await handler.Filter(context, next),
                () =>
                {
                    allowed = true;
                    return default;
                });

            return allowed;
        }


        private delegate ValueTask MessageHandlerFunc(IMessageContext context);


        private MessageHandlerFunc WrapMethod(MethodInfo method, IEnumerable<ValueFactory> parameterFactories, ResultHandler? resultHandler)
        {
            if (resultHandler != null)
                return WrapResultHandlerMethod(method.CreateExpressionInvoke(), parameterFactories, resultHandler);

            if (method.ReturnType == typeof(void))
                return WrapNullMethod(method.CreateExpressionInvoke(), parameterFactories);

            if (method.ReturnType == typeof(Task))
                return WrapTaskMethod(method.CreateExpressionInvoke(), parameterFactories);

            if (method.ReturnType == typeof(ValueTask))
                return WrapValueTaskMethod(method.CreateExpressionInvoke(), parameterFactories);

            // Breaking change in Tapeti 2.9: PublishResultBinding or other middleware should have taken care of the return value. If not, don't silently discard it.
            throw new ArgumentException($"Method {method.Name} on controller {method.DeclaringType?.FullName} returns type {method.ReturnType.FullName}, which can not be handled by Tapeti or any registered middleware");
        }


        private MessageHandlerFunc WrapResultHandlerMethod(ExpressionInvoke invoke, IEnumerable<ValueFactory> parameterFactories, ResultHandler resultHandler)
        {
            return context =>
            {
                var controllerPayload = context.Get<ControllerMessageContextPayload>();
                try
                {
                    var result = invoke(controllerPayload.Controller, parameterFactories.Select(p => p(context)).ToArray());
                    return resultHandler(context, result);
                }
                catch (Exception e)
                {
                    AddExceptionData(e);
                    throw;
                }
            };
        }

        private MessageHandlerFunc WrapNullMethod(ExpressionInvoke invoke, IEnumerable<ValueFactory> parameterFactories)
        {
            return context =>
            {
                var controllerPayload = context.Get<ControllerMessageContextPayload>();
                try
                {
                    invoke(controllerPayload.Controller, parameterFactories.Select(p => p(context)).ToArray());
                    return default;
                }
                catch (Exception e)
                {
                    AddExceptionData(e);
                    throw;
                }
            };
        }


        private MessageHandlerFunc WrapTaskMethod(ExpressionInvoke invoke, IEnumerable<ValueFactory> parameterFactories)
        {
            return context =>
            {
                var controllerPayload = context.Get<ControllerMessageContextPayload>();
                try
                {
                    return new ValueTask((Task) invoke(controllerPayload.Controller, parameterFactories.Select(p => p(context)).ToArray()));
                }
                catch (Exception e)
                {
                    AddExceptionData(e);
                    throw;
                }
            };
        }


        private MessageHandlerFunc WrapValueTaskMethod(ExpressionInvoke invoke, IEnumerable<ValueFactory> parameterFactories)
        {
            return context =>
            {
                var controllerPayload = context.Get<ControllerMessageContextPayload>();
                try
                {
                    return (ValueTask)invoke(controllerPayload.Controller, parameterFactories.Select(p => p(context)).ToArray());
                }
                catch (Exception e)
                {
                    AddExceptionData(e);
                    throw;
                }
            };
        }


        private void AddExceptionData(Exception exception)
        {
            exception.Data["Tapeti.Controller.Name"] = bindingInfo.ControllerType.FullName;
            exception.Data["Tapeti.Controller.Method"] = bindingInfo.Method.Name;
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
            /// Optional arguments (x-arguments) passed when declaring the queue.
            /// </summary>
            public IRabbitMQArguments? QueueArguments { get; set; }
                
            /// <summary>
            /// Determines if the QueueInfo properties contain a valid combination.
            /// </summary>
            public bool IsValid => QueueType == QueueType.Dynamic || !string.IsNullOrEmpty(Name);


            public QueueInfo(QueueType queueType, string name)
            {
                QueueType = queueType;
                Name = name;
            }
        }
    }
}
