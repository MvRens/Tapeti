using System;
using System.Text;
using Tapeti.Config;
using Tapeti.Connection;
using Xunit.Abstractions;

namespace Tapeti.Tests.Mock
{
    internal class MockLogger : IBindingLogger
    {
        private readonly ITestOutputHelper testOutputHelper;


        public MockLogger(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }


        public void Connect(IConnectContext connectContext)
        {
            testOutputHelper.WriteLine($"{(connectContext.IsReconnect ? "Reconnecting" : "Connecting")} to {connectContext.ConnectionParams.HostName}:{connectContext.ConnectionParams.Port}{connectContext.ConnectionParams.VirtualHost}");
        }

        public void ConnectFailed(IConnectFailedContext connectContext)
        {
            testOutputHelper.WriteLine($"Connection failed: {connectContext.Exception}");
        }

        public void ConnectSuccess(IConnectSuccessContext connectContext)
        {
            testOutputHelper.WriteLine($"{(connectContext.IsReconnect ? "Reconnected" : "Connected")} using local port {connectContext.LocalPort}");
        }

        public void Disconnect(IDisconnectContext disconnectContext)
        {
            testOutputHelper.WriteLine($"Connection closed: {(!string.IsNullOrEmpty(disconnectContext.ReplyText) ? disconnectContext.ReplyText : "<no reply text>")} (reply code: {disconnectContext.ReplyCode})");
        }

        public void ConsumeException(Exception exception, IMessageContext messageContext, ConsumeResult consumeResult)
        {
            testOutputHelper.WriteLine(exception.Message);
        }

        public void QueueDeclare(string queueName, bool durable, bool passive)
        {
            testOutputHelper.WriteLine(passive
                ? $"Verifying durable queue {queueName}"
                : $"Declaring {(durable ? "durable" : "dynamic")} queue {queueName}");
        }

        public void QueueExistsWarning(string queueName, IRabbitMQArguments existingArguments, IRabbitMQArguments arguments)
        {
            var argumentsText = new StringBuilder();
            foreach (var pair in arguments)
            {
                if (argumentsText.Length > 0)
                    argumentsText.Append(", ");

                argumentsText.Append($"{pair.Key} = {pair.Value}");
            }

            testOutputHelper.WriteLine($"Durable queue {queueName} exists with incompatible x-arguments ({argumentsText}) and will not be redeclared, queue will be consumed as-is");
        }

        public void QueueBind(string queueName, bool durable, string exchange, string routingKey)
        {
            testOutputHelper.WriteLine($"Binding {queueName} to exchange {exchange} with routing key {routingKey}");
        }

        public void QueueUnbind(string queueName, string exchange, string routingKey)
        {
            testOutputHelper.WriteLine($"Removing binding for {queueName} to exchange {exchange} with routing key {routingKey}");
        }

        public void ExchangeDeclare(string exchange)
        {
            testOutputHelper.WriteLine($"Declaring exchange {exchange}");
        }

        public void QueueObsolete(string queueName, bool deleted, uint messageCount)
        {
            testOutputHelper.WriteLine(deleted
                ? $"Obsolete queue was deleted: {queueName}"
                : $"Obsolete queue bindings removed: {queueName}, {messageCount} messages remaining");
        }
    }
}
