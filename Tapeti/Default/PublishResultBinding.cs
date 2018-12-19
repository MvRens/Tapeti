﻿using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using RabbitMQ.Client.Framing;
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


            if (!context.Result.Info.ParameterType.IsTypeOrTaskOf(t => t.IsClass, out var isTaskOf, out var actualType))
                return;


            if (isTaskOf)
            {
                var handler = GetType().GetMethod("PublishGenericTaskResult", BindingFlags.NonPublic | BindingFlags.Static)?.MakeGenericMethod(actualType);
                Debug.Assert(handler != null, nameof(handler) + " != null");

                context.Result.SetHandler(async (messageContext, value) =>
                {
                    await (Task)handler.Invoke(null, new[] { messageContext, value });
                });
            }
            else
                context.Result.SetHandler((messageContext, value) => 
                    value == null ? null : Reply(value, messageContext));
        }


        private static Task Reply(object message, IMessageContext messageContext)
        {
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
