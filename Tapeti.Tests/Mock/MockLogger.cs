using System;
using System.Text;
using Tapeti.Config;
using Tapeti.Connection;
using Xunit.Abstractions;

namespace Tapeti.Tests.Mock
{
    internal class MockLogger : IBindingLogger, IChannelLogger
    {
        private readonly ITestOutputHelper testOutputHelper;


        public MockLogger(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }


        public void Connect(ConnectContext connectContext)
        {
            WriteLine($"{(connectContext.IsReconnect ? "Reconnecting" : "Connecting")} to {connectContext.ConnectionParams.HostName}:{connectContext.ConnectionParams.Port}{connectContext.ConnectionParams.VirtualHost}");
        }

        public void ConnectFailed(ConnectFailedContext connectContext)
        {
            WriteLine($"Connection failed: {connectContext.Exception}");
        }

        public void ConnectSuccess(ConnectSuccessContext connectContext)
        {
            WriteLine($"{(connectContext.IsReconnect ? "Reconnected" : "Connected")} using local port {connectContext.LocalPort}");
        }

        public void Disconnect(DisconnectContext disconnectContext)
        {
            WriteLine($"Connection closed: {(!string.IsNullOrEmpty(disconnectContext.ReplyText) ? disconnectContext.ReplyText : "<no reply text>")} (reply code: {disconnectContext.ReplyCode})");
        }

        public void ConsumeException(Exception exception, IMessageContext messageContext, ConsumeResult consumeResult)
        {
            WriteLine(exception.Message);
        }

        public void QueueDeclare(string queueName, bool durable, bool passive)
        {
            WriteLine(passive
                ? $"Verifying durable queue {queueName}"
                : $"Declaring {(durable ? "durable" : "dynamic")} queue {queueName}");
        }

        public void QueueExistsWarning(string queueName, IRabbitMQArguments? existingArguments, IRabbitMQArguments? arguments)
        {
            WriteLine($"[Tapeti] Durable queue {queueName} exists with incompatible x-arguments ({GetArgumentsText(existingArguments)} vs. {GetArgumentsText(arguments)}) and will not be redeclared, queue will be consumed as-is");
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


        public void QueueBind(string queueName, bool durable, string exchange, string routingKey)
        {
            WriteLine($"Binding {queueName} to exchange {exchange} with routing key {routingKey}");
        }

        public void QueueUnbind(string queueName, string exchange, string routingKey)
        {
            WriteLine($"Removing binding for {queueName} to exchange {exchange} with routing key {routingKey}");
        }

        public void ExchangeDeclare(string exchange)
        {
            WriteLine($"Declaring exchange {exchange}");
        }

        public void QueueObsolete(string queueName, bool deleted, uint messageCount)
        {
            WriteLine(deleted
                ? $"Obsolete queue was deleted: {queueName}"
                : $"Obsolete queue bindings removed: {queueName}, {messageCount} messages remaining");
        }


        public void ChannelCreated(ChannelCreatedContext context)
        {
            WriteLine($"Channel #{context.ChannelNumber} on connection {context.ConnectionReference} of type {context.ChannelType} {(context.IsRecreate ? "re-" : "")}created");
        }

        public void ChannelShutdown(ChannelShutdownContext context)
        {
            WriteLine($"Channel #{context.ChannelNumber} on connection {context.ConnectionReference} of type {context.ChannelType} shut down, code {context.ReplyCode}: {context.ReplyText}");
        }


        private void WriteLine(string message)
        {
            testOutputHelper.WriteLine($"[{DateTime.Now.ToLongTimeString()}] {message}");
        }
    }
}
