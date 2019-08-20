using System;
using Tapeti.Config;
using ISerilogLogger = Serilog.ILogger;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Serilog
{
    /// <inheritdoc />
    /// <summary>
    /// Implements the Tapeti ILogger interface for Serilog output.
    /// </summary>
    public class TapetiSeriLogger: ILogger
    {
        private readonly ISerilogLogger seriLogger;


        /// <inheritdoc />
        public TapetiSeriLogger(ISerilogLogger seriLogger)
        {
            this.seriLogger = seriLogger;
        }


        /// <inheritdoc />
        public void Connect(TapetiConnectionParams connectionParams, bool isReconnect)
        {
            seriLogger
                .ForContext("isReconnect", isReconnect)
                .Information("Tapeti: trying to connect to {host}:{port}/{virtualHost}", 
                    connectionParams.HostName,
                    connectionParams.Port,
                    connectionParams.VirtualHost);
        }

        /// <inheritdoc />
        public void ConnectFailed(TapetiConnectionParams connectionParams, Exception exception)
        {
            seriLogger.Error(exception, "Tapeti: could not connect to {host}:{port}/{virtualHost}", 
                connectionParams.HostName,
                connectionParams.Port,
                connectionParams.VirtualHost);
        }

        /// <inheritdoc />
        public void ConnectSuccess(TapetiConnectionParams connectionParams, bool isReconnect)
        {
            seriLogger
                .ForContext("isReconnect", isReconnect)
                .Information("Tapeti: successfully connected to {host}:{port}/{virtualHost}", 
                    connectionParams.HostName,
                    connectionParams.Port,
                    connectionParams.VirtualHost);
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
        public void QueueObsolete(string queueName, bool deleted, uint messageCount)
        {
            if (deleted)
                seriLogger.Information("Tapeti: obsolete queue {queue} has been deleted", queueName);
            else
                seriLogger.Information("Tapeti: obsolete queue {queue} has been unbound but not yet deleted, {messageCount} messages remaining", queueName, messageCount);
        }
    }
}
