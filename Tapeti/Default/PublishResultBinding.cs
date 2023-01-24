using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Annotations;
using Tapeti.Config;
using Tapeti.Helpers;

namespace Tapeti.Default
{
    /// <summary>
    /// Attempts to publish a return value for Controller methods as a response to the incoming message.
    /// </summary>
    public class PublishResultBinding : IControllerBindingMiddleware
    {
        /// <inheritdoc />
        public void Handle(IControllerBindingContext context, Action next)
        {
            next();

            if (context.Result.HasHandler)
                return;


            var hasClassResult = context.Result.Info.ParameterType.IsTypeOrTaskOf(t => t.IsClass, out var taskType, out var actualType);
            
            var request = context.MessageClass?.GetCustomAttribute<RequestAttribute>();
            var expectedClassResult = request?.Response;

            // Verify the return type matches with the Request attribute of the message class. This is a backwards incompatible change in
            // Tapeti 1.2: if you just want to publish another message as a result of the incoming message, explicitly call IPublisher.Publish.
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (!hasClassResult && expectedClassResult != null || hasClassResult && expectedClassResult != actualType)
               throw new ArgumentException($"Message handler for non-request message type {context.MessageClass?.FullName} must return type {expectedClassResult?.FullName ?? "void"} in controller {context.Method.DeclaringType?.FullName}, method {context.Method.Name}, found: {actualType.FullName ?? "void"}");

            if (!hasClassResult)
                return;



            switch (taskType)
            {
                case TaskType.None:
                    context.Result.SetHandler((messageContext, value) => Reply(value, messageContext));
                    break;

                case TaskType.Task:
                    var handler = GetType().GetMethod(nameof(PublishGenericTaskResult), BindingFlags.NonPublic | BindingFlags.Static)?.MakeGenericMethod(actualType);
                    Debug.Assert(handler != null, nameof(handler) + " != null");

                    context.Result.SetHandler((messageContext, value) =>
                    {
                        var result = handler.Invoke(null, new[] { messageContext, value });
                        return result != null ? (ValueTask)result : ValueTask.CompletedTask;
                    });
                    break;

                case TaskType.ValueTask:
                    var valueTaskHandler = GetType().GetMethod(nameof(PublishGenericValueTaskResult), BindingFlags.NonPublic | BindingFlags.Static)?.MakeGenericMethod(actualType);
                    Debug.Assert(valueTaskHandler != null, nameof(handler) + " != null");

                    context.Result.SetHandler((messageContext, value) =>
                    {
                        var result = valueTaskHandler.Invoke(null, new[] { messageContext, value });
                        return result != null ? (ValueTask)result : ValueTask.CompletedTask;
                    });
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }



        private static async ValueTask PublishGenericTaskResult<T>(IMessageContext messageContext, object value) where T : class
        {
            var message = await (Task<T>)value;
            await Reply(message, messageContext);
        }


        private static async ValueTask PublishGenericValueTaskResult<T>(IMessageContext messageContext, object value) where T : class
        {
            var message = await (ValueTask<T>)value;
            await Reply(message, messageContext);
        }


        private static async ValueTask Reply(object? message, IMessageContext messageContext)
        {
            if (message == null)
                throw new ArgumentException("Return value of a request message handler must not be null");

            var publisher = (IInternalPublisher)messageContext.Config.DependencyResolver.Resolve<IPublisher>();
            var properties = new MessageProperties
            {
                CorrelationId = messageContext.Properties.CorrelationId
            };

            if (!string.IsNullOrEmpty(messageContext.Properties.ReplyTo))
                await publisher.PublishDirect(message, messageContext.Properties.ReplyTo, properties, messageContext.Properties.Persistent.GetValueOrDefault(true));
            else
                await publisher.Publish(message, properties, false);
        }
    }
}
