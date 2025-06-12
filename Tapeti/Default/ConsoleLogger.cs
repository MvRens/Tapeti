using System;
using System.Text;
using JetBrains.Annotations;
using Tapeti.Config;
using Tapeti.Connection;

// ReSharper disable UnusedMember.Global - public API

namespace Tapeti.Default
{
    /// <summary>
    /// Default ILogger implementation for console applications.
    /// </summary>
    public class ConsoleLogger : IBindingLogger, IChannelLogger
    {
        /// <summary>
        /// Default ILogger implementation for console applications. This version
        /// includes the message body if available when an error occurs.
        /// </summary>
        [PublicAPI]
        public class WithMessageLogging : ConsoleLogger
        {
            internal override bool IncludeMessageBody() => true;
        }


        /// <inheritdoc />
        public void Connect(ConnectContext context)
        {
            Console.WriteLine($"[Tapeti] {(context.IsReconnect ? "Reconnecting" : "Connecting")} to {context.ConnectionParams.HostName}:{context.ConnectionParams.Port}{context.ConnectionParams.VirtualHost}");
        }

        /// <inheritdoc />
        public void ConnectFailed(ConnectFailedContext context)
        {
            Console.WriteLine($"[Tapeti] Connection failed: {context.Exception}");
        }

        /// <inheritdoc />
        public void ConnectSuccess(ConnectSuccessContext context)
        {
            Console.WriteLine($"[Tapeti] {(context.IsReconnect ? "Reconnected" : "Connected")} using local port {context.LocalPort}");
        }

        /// <inheritdoc />
        public void Disconnect(DisconnectContext context)
        {
            Console.WriteLine($"[Tapeti] Connection closed: {(!string.IsNullOrEmpty(context.ReplyText) ? context.ReplyText : "<no reply text>")} (reply code: {context.ReplyCode})");
        }


        /// <inheritdoc />
        public void ConsumeStarted(ConsumeStartedContext context)
        {
            Console.WriteLine($"[Tapeti] Consumer {(context.IsRestart ? "restarted" : "started")} for {(context.IsDynamicQueue ? "dynamic queue" : "durable queue")} {context.QueueName}");
        }


        /// <inheritdoc />
        public void ConsumeException(Exception exception, IMessageContext messageContext, ConsumeResult consumeResult)
        {
            Console.WriteLine("[Tapeti] Exception while handling message");
            Console.WriteLine($"  Result        : {consumeResult}");
            Console.WriteLine($"  Exchange      : {messageContext.Exchange}");
            Console.WriteLine($"  Queue         : {messageContext.Queue}");
            Console.WriteLine($"  RoutingKey    : {messageContext.RoutingKey}");
            Console.WriteLine($"  ReplyTo       : {messageContext.Properties.ReplyTo}");
            Console.WriteLine($"  CorrelationId : {messageContext.Properties.CorrelationId}");

            if (messageContext.TryGet<ControllerMessageContextPayload>(out var controllerPayload))
            {
                Console.WriteLine($"  Controller    : {controllerPayload.Binding.Controller.FullName}");
                Console.WriteLine($"  Method        : {controllerPayload.Binding.Method.Name}");
            }

            if (IncludeMessageBody())
                Console.WriteLine($"  Body          : {(messageContext.RawBody != null ? Encoding.UTF8.GetString(messageContext.RawBody) : "<null>")}");


            Console.WriteLine();
            Console.WriteLine(exception);
        }

        /// <inheritdoc />
        public void QueueDeclare(string queueName, bool durable, bool passive)
        {
            Console.WriteLine(passive
                ? $"[Tapeti] Verifying durable queue {queueName}"
                : $"[Tapeti] Declaring {(durable ? "durable" : "dynamic")} queue {queueName}");
        }

        /// <inheritdoc />
        public void QueueExistsWarning(string queueName, IRabbitMQArguments? existingArguments, IRabbitMQArguments? arguments)
        {
            Console.WriteLine($"[Tapeti] Durable queue {queueName} exists with incompatible x-arguments ({GetArgumentsText(existingArguments)} vs. {GetArgumentsText(arguments)}) and will not be redeclared, queue will be consumed as-is");
        }


        private static string GetArgumentsText(IRabbitMQArguments? arguments)
        {
            if (arguments == null || arguments.Count == 0)
                return "empty";

            var argumentsText = new StringBuilder();
            foreach (var pair in arguments)
            {
                if (argumentsText.Length > 0)
                    argumentsText.Append(", ");

                argumentsText.Append($"{pair.Key} = {pair.Value}");
            }

            return argumentsText.ToString();
        }


        /// <inheritdoc />
        public void QueueBind(string queueName, bool durable, string exchange, string routingKey)
        {
            Console.WriteLine($"[Tapeti] Binding {queueName} to exchange {exchange} with routing key {routingKey}");
        }

        /// <inheritdoc />
        public void QueueUnbind(string queueName, string exchange, string routingKey)
        {
            Console.WriteLine($"[Tapeti] Removing binding for {queueName} to exchange {exchange} with routing key {routingKey}");
        }

        /// <inheritdoc />
        public void ExchangeDeclare(string exchange)
        {
            Console.WriteLine($"[Tapeti] Declaring exchange {exchange}");
        }

        /// <inheritdoc />
        public void QueueObsolete(string queueName, bool deleted, uint messageCount)
        {
            Console.WriteLine(deleted
                ? $"[Tapeti] Obsolete queue was deleted: {queueName}"
                : $"[Tapeti] Obsolete queue bindings removed: {queueName}, {messageCount} messages remaining");
        }


        /// <inheritdoc />
        public void ChannelCreated(ChannelCreatedContext context)
        {
            Console.WriteLine($"[Tapeti] Channel #{context.ChannelNumber} on connection {context.ConnectionReference} of type {context.ChannelType} {(context.IsRecreate ? "re-" : "")}created");
        }


        /// <inheritdoc />
        public void ChannelShutdown(ChannelShutdownContext context)
        {
            Console.WriteLine($"[Tapeti] Channel #{context.ChannelNumber} on connection {context.ConnectionReference} of type {context.ChannelType} shut down, code {context.ReplyCode}: {context.ReplyText}");
        }


        internal virtual bool IncludeMessageBody() => false;
    }
}
