using System;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <inheritdoc />
    /// <summary>
    /// Default ILogger implementation for console applications.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
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
            Console.WriteLine($"  Result     : {consumeResult}");
            Console.WriteLine($"  Exchange   : {messageContext.Exchange}");
            Console.WriteLine($"  Queue      : {messageContext.Queue}");
            Console.WriteLine($"  RoutingKey : {messageContext.RoutingKey}");

            if (messageContext is IControllerMessageContext controllerMessageContext)
            {
                Console.WriteLine($"  Controller : {controllerMessageContext.Binding.Controller.FullName}");
                Console.WriteLine($"  Method     : {controllerMessageContext.Binding.Method.Name}");
            }

            Console.WriteLine();
            Console.WriteLine(exception);
        }

        /// <inheritdoc />
        public void QueueObsolete(string queueName, bool deleted, uint messageCount)
        {
            Console.WriteLine(deleted 
                ? $"[Tapeti] Obsolete queue was deleted: {queueName}" 
                : $"[Tapeti] Obsolete queue bindings removed: {queueName}, {messageCount} messages remaining");
        }
    }
}
