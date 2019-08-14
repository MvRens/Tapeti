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
        public void Connect(TapetiConnectionParams connectionParams, bool isReconnect)
        {
            Console.WriteLine($"[Tapeti] {(isReconnect ? "Reconnecting" : "Connecting")} to {connectionParams.HostName}:{connectionParams.Port}{connectionParams.VirtualHost}");
        }

        /// <inheritdoc />
        public void ConnectFailed(TapetiConnectionParams connectionParams, Exception exception)
        {
            Console.WriteLine($"[Tapeti] Connection failed: {exception}");
        }

        /// <inheritdoc />
        public void ConnectSuccess(TapetiConnectionParams connectionParams, bool isReconnect)
        {
            Console.WriteLine($"[Tapeti] {(isReconnect ? "Reconnected" : "Connected")}");
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
    }
}
