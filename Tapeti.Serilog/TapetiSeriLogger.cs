using System;
using System.Collections.Generic;
using Tapeti.Config;
using ISerilogLogger = Serilog.ILogger;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Serilog
{
    /// <summary>
    /// Implements the Tapeti ILogger interface for Serilog output.
    /// </summary>
    public class TapetiSeriLogger: IBindingLogger
    {
        private readonly ISerilogLogger seriLogger;


        /// <summary>
        /// Create a Tapeti ILogger implementation to output to the specified Serilog.ILogger interface
        /// </summary>
        /// <param name="seriLogger">The Serilog.ILogger implementation to output Tapeti log message to</param>
        public TapetiSeriLogger(ISerilogLogger seriLogger)
        {
            this.seriLogger = seriLogger;
        }


        /// <inheritdoc />
        public void Connect(IConnectContext connectContext)
        {
            seriLogger
                .ForContext("isReconnect", connectContext.IsReconnect)
                .Information("Tapeti: trying to connect to {host}:{port}/{virtualHost}", 
                    connectContext.ConnectionParams.HostName,
                    connectContext.ConnectionParams.Port,
                    connectContext.ConnectionParams.VirtualHost);
        }

        /// <inheritdoc />
        public void ConnectFailed(IConnectFailedContext connectContext)
        {
            seriLogger.Error(connectContext.Exception, "Tapeti: could not connect to {host}:{port}/{virtualHost}",
                connectContext.ConnectionParams.HostName,
                connectContext.ConnectionParams.Port,
                connectContext.ConnectionParams.VirtualHost);
        }

        /// <inheritdoc />
        public void ConnectSuccess(IConnectSuccessContext connectContext)
        {
            seriLogger
                .ForContext("isReconnect", connectContext.IsReconnect)
                .Information("Tapeti: successfully connected to {host}:{port}/{virtualHost} on local port {localPort}",
                    connectContext.ConnectionParams.HostName,
                    connectContext.ConnectionParams.Port,
                    connectContext.ConnectionParams.VirtualHost,
                    connectContext.LocalPort);
        }

        /// <inheritdoc />
        public void Disconnect(IDisconnectContext disconnectContext)
        {
            seriLogger
                .Information("Tapeti: connection closed, reply text = {replyText}, reply code = {replyCode}",
                    disconnectContext.ReplyText,
                    disconnectContext.ReplyCode);
        }

        /// <inheritdoc />
        public void ConsumeException(Exception exception, IMessageContext messageContext, ConsumeResult consumeResult)
        {
            var contextLogger = seriLogger
                .ForContext("consumeResult", consumeResult)
                .ForContext("exchange", messageContext.Exchange)
                .ForContext("queue", messageContext.Queue)
                .ForContext("routingKey", messageContext.RoutingKey);

            if (messageContext is IControllerMessageContext controllerMessageContext)
            {
                contextLogger = contextLogger
                    .ForContext("controller", controllerMessageContext.Binding.Controller.FullName)
                    .ForContext("method", controllerMessageContext.Binding.Method.Name);
            }
            
            contextLogger.Error(exception, "Tapeti: exception in message handler");
        }

        /// <inheritdoc />
        public void QueueDeclare(string queueName, bool durable, bool passive)
        {
            if (passive)
                seriLogger.Information("Tapeti: verifying durable queue {queueName}", queueName);
            else 
                seriLogger.Information("Tapeti: declaring {queueType} queue {queueName}", durable ? "durable" : "dynamic", queueName);
        }

        /// <inheritdoc />
        public void QueueExistsWarning(string queueName, Dictionary<string, string> arguments)
        {
            seriLogger.Warning("Tapeti: durable queue {queueName} exists with incompatible x-arguments ({arguments}) and will not be redeclared, queue will be consumed as-is",
                queueName,
                arguments);
        }

        /// <inheritdoc />
        public void QueueBind(string queueName, bool durable, string exchange, string routingKey)
        {
            seriLogger.Information("Tapeti: binding {queueName} to exchange {exchange} with routing key {routingKey}",
                queueName,
                exchange,
                routingKey);
        }

        /// <inheritdoc />
        public void QueueUnbind(string queueName, string exchange, string routingKey)
        {
            seriLogger.Information("Tapeti: removing binding for {queueName} to exchange {exchange} with routing key {routingKey}",
                queueName,
                exchange,
                routingKey);
        }

        /// <inheritdoc />
        public void ExchangeDeclare(string exchange)
        {
            seriLogger.Information("Tapeti: declaring exchange {exchange}", exchange);
        }

        /// <inheritdoc />
        public void QueueObsolete(string queueName, bool deleted, uint messageCount)
        {
            if (deleted)
                seriLogger.Information("Tapeti: obsolete queue {queue} has been deleted", queueName);
            else
                seriLogger.Information("Tapeti: obsolete queue {queue} has been unbound but not yet deleted, {messageCount} messages remaining", queueName, messageCount);
        }
    }
}
