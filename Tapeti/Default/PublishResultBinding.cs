using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using RabbitMQ.Client.Framing;
using Tapeti.Annotations;
using Tapeti.Config;
using Tapeti.Helpers;

namespace Tapeti.Default
{
    public class PublishResultBinding : IBindingMiddleware
    {
        public void Handle(IBindingContext context, Action next)
        {
            next();

            if (context.Result.HasHandler)
                return;


            var hasClassResult = context.Result.Info.ParameterType.IsTypeOrTaskOf(t => t.IsClass, out var isTaskOf, out var actualType);
            
            var request = context.MessageClass?.GetCustomAttribute<RequestAttribute>();
            var expectedClassResult = request?.Response;

            // Verify the return type matches with the Request attribute of the message class. This is a backwards incompatible change in
            // Tapeti 1.2: if you just want to publish another message as a result of the incoming message, explicitly call IPublisher.Publish.
            if (!hasClassResult && expectedClassResult != null || hasClassResult && expectedClassResult != actualType)
               throw new ArgumentException($"Message handler must return type {expectedClassResult?.FullName ?? "void"} in controller {context.Method.DeclaringType?.FullName}, method {context.Method.Name}, found: {actualType?.FullName ?? "void"}");

            if (!hasClassResult)
                return;



            if (isTaskOf)
            {
                var handler = GetType().GetMethod("PublishGenericTaskResult", BindingFlags.NonPublic | BindingFlags.Static)?.MakeGenericMethod(actualType);
                Debug.Assert(handler != null, nameof(handler) + " != null");

                context.Result.SetHandler(async (messageContext, value) => { await (Task) handler.Invoke(null, new[] {messageContext, value }); });
            }
            else
                context.Result.SetHandler((messageContext, value) => Reply(value, messageContext));
        }



        // ReSharper disable once UnusedMember.Local - used implicitly above
        private static async Task PublishGenericTaskResult<T>(IMessageContext messageContext, object value) where T : class
        {
            var message = await (Task<T>)value;
            await Reply(message, messageContext);
        }


        private static Task Reply(object message, IMessageContext messageContext)
        {
            if (message == null)
                throw new ArgumentException("Return value of a request message handler must not be null");

            var publisher = (IInternalPublisher)messageContext.DependencyResolver.Resolve<IPublisher>();
            var properties = new BasicProperties();

            // Only set the property if it's not null, otherwise a string reference exception can occur:
            // http://rabbitmq.1065348.n5.nabble.com/SocketException-when-invoking-model-BasicPublish-td36330.html
            if (messageContext.Properties.IsCorrelationIdPresent())
                properties.CorrelationId = messageContext.Properties.CorrelationId;

            if (messageContext.Properties.IsReplyToPresent())
                return publisher.PublishDirect(message, messageContext.Properties.ReplyTo, properties);

            return publisher.Publish(message, properties);
        }
    }
}
