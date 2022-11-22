using System;
using System.Text;
using Tapeti.Config;
using Tapeti.Connection;

// ReSharper disable UnusedMember.Global - public API

namespace Tapeti.Default
{
    /// <summary>
    /// Default ILogger implementation for console applications.
    /// </summary>
    public class ConsoleLogger : IBindingLogger
    {
        /// <summary>
        /// Default ILogger implementation for console applications. This version
        /// includes the message body if available when an error occurs.
        /// </summary>
        public class WithMessageLogging : ConsoleLogger
        {
            internal override bool IncludeMessageBody() => true;
        }

        
        /// <inheritdoc />
        public void Connect(IConnectContext connectContext)
        {
            Console.WriteLine($"[Tapeti] {(connectContext.IsReconnect ? "Reconnecting" : "Connecting")} to {connectContext.ConnectionParams.HostName}:{connectContext.ConnectionParams.Port}{connectContext.ConnectionParams.VirtualHost}");
        }

        /// <inheritdoc />
        public void ConnectFailed(IConnectFailedContext connectContext)
        {
            Console.WriteLine($"[Tapeti] Connection failed: {connectContext.Exception}");
        }

        /// <inheritdoc />
        public void ConnectSuccess(IConnectSuccessContext connectContext)
        {
            Console.WriteLine($"[Tapeti] {(connectContext.IsReconnect ? "Reconnected" : "Connected")} using local port {connectContext.LocalPort}");
        }

        /// <inheritdoc />
        public void Disconnect(IDisconnectContext disconnectContext)
        {
            Console.WriteLine($"[Tapeti] Connection closed: {(!string.IsNullOrEmpty(disconnectContext.ReplyText) ? disconnectContext.ReplyText : "<no reply text>")} (reply code: {disconnectContext.ReplyCode})");
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
        public void QueueExistsWarning(string queueName, IRabbitMQArguments existingArguments, IRabbitMQArguments arguments)
        {
            Console.WriteLine($"[Tapeti] Durable queue {queueName} exists with incompatible x-arguments ({GetArgumentsText(existingArguments)} vs. {GetArgumentsText(arguments)}) and will not be redeclared, queue will be consumed as-is");
        }


        private static string GetArgumentsText(IRabbitMQArguments arguments)
        {
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

        internal virtual bool IncludeMessageBody() => false;
    }
}
