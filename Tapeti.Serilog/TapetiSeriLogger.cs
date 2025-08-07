using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using Serilog.Events;
using Tapeti.Config;
using Tapeti.Connection;
using ISerilogLogger = Serilog.ILogger;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Serilog
{
    /// <summary>
    /// Implements the Tapeti ILogger interface for Serilog output.
    /// </summary>
    [PublicAPI]
    public class TapetiSeriLogger: IBindingLogger, IChannelLogger
    {
        /// <summary>
        /// Implements the Tapeti ILogger interface for Serilog output. This version
        /// includes the message body and information if available when an error occurs.
        /// </summary>
        public class WithMessageLogging : TapetiSeriLogger
        {
            /// <inheritdoc />
            [Obsolete("Use IncludeMessageInfo and IncludeMessageBody properties instead")]
            public WithMessageLogging(ISerilogLogger seriLogger) : base(seriLogger)
            {
                IncludeMessageInfo = true;
                IncludeMessageBody = true;
            }
        }




        private readonly ISerilogLogger seriLogger;

        /// <summary>
        /// Determines if the message properties are included in the log message when an exception occurs.
        /// </summary>
        public bool IncludeMessageInfo { get; init; }

        /// <summary>
        /// Determines if the message body is included in the log message when an exception occurs.
        /// </summary>
        public bool IncludeMessageBody { get; init; }


        /// <summary>
        /// Determines the log level for informational connection events (e.g. connecting, connected, disconnected).
        /// </summary>
        /// <remarks>
        /// Connection failed events are currently always logged as Error.
        /// </remarks>
        public LogEventLevel ConnectionEventsLevel { get; init; } = LogEventLevel.Information;

        /// <summary>
        /// Determines the log level for informational consumers events (consumer started).
        /// </summary>
        /// <remarks>
        /// Exceptions in message handlers are currently always logged as Error.
        /// </remarks>
        public LogEventLevel ConsumerEventsLevel { get; init; } = LogEventLevel.Information;

        /// <summary>
        /// Determines the log level for informational binding events (e.g. declaring queues, exchanges and bindings).
        /// </summary>
        /// <remarks>
        /// Messages about queues marked as Obsolete are currently always logged as Information.
        /// Messages about existing queues with incompatible arguments are always logged as Warning.
        /// </remarks>
        public LogEventLevel BindingEventsLevel { get; init; } = LogEventLevel.Information;

        /// <summary>
        /// Determines the log level for informational channel events (channel created or shut down).
        /// </summary>
        public LogEventLevel ChannelEventsLevel { get; init; } = LogEventLevel.Information;


        /// <summary>
        /// Create a Tapeti ILogger implementation to output to the specified Serilog.ILogger interface
        /// </summary>
        /// <param name="seriLogger">The Serilog.ILogger implementation to output Tapeti log message to</param>
        public TapetiSeriLogger(ISerilogLogger seriLogger)
        {
            this.seriLogger = seriLogger;
        }


        /// <inheritdoc />
        public void Connect(ConnectContext context)
        {
            seriLogger
                .ForContext("isReconnect", context.IsReconnect)
                .Write(ConnectionEventsLevel, "Tapeti: trying to connect to {host}:{port}/{virtualHost}",
                    context.ConnectionParams.HostName,
                    context.ConnectionParams.Port,
                    context.ConnectionParams.VirtualHost);
        }

        /// <inheritdoc />
        public void ConnectFailed(ConnectFailedContext context)
        {
            seriLogger.Error(context.Exception, "Tapeti: could not connect to {host}:{port}/{virtualHost}",
                context.ConnectionParams.HostName,
                context.ConnectionParams.Port,
                context.ConnectionParams.VirtualHost);
        }

        /// <inheritdoc />
        public void ConnectSuccess(ConnectSuccessContext context)
        {
            seriLogger
                .ForContext("isReconnect", context.IsReconnect)
                .Write(ConnectionEventsLevel, "Tapeti: successfully connected to {host}:{port}/{virtualHost} on local port {localPort}",
                    context.ConnectionParams.HostName,
                    context.ConnectionParams.Port,
                    context.ConnectionParams.VirtualHost,
                    context.LocalPort);
        }

        /// <inheritdoc />
        public void Disconnect(DisconnectContext context)
        {
            seriLogger
                .Write(ConnectionEventsLevel, "Tapeti: connection closed, reply text = {replyText}, reply code = {replyCode}",
                    context.ReplyText,
                    context.ReplyCode);
        }

        /// <inheritdoc />
        public void ConsumeStarted(ConsumeStartedContext context)
        {
            seriLogger
                .Write(ConsumerEventsLevel, "Tapeti: consumer {startType} for {queueType} {queueName}",
                    context.IsRestart ? "restarted" : "started",
                    context.IsDynamicQueue ? "dynamic queue" : "durable queue",
                    context.QueueName);
        }

        /// <inheritdoc />
        public void ConsumeException(Exception exception, IMessageContext messageContext, ConsumeResult consumeResult)
        {
            var message = new StringBuilder("Tapeti: exception in message handler");
            var messageParams = new List<object?>();

            var contextLogger = seriLogger
                .ForContext("consumeResult", consumeResult)
                .ForContext("exchange", messageContext.Exchange)
                .ForContext("queue", messageContext.Queue)
                .ForContext("routingKey", messageContext.RoutingKey);

            if (messageContext.TryGet<ControllerMessageContextPayload>(out var controllerPayload))
            {
                contextLogger = contextLogger
                    .ForContext("controller", controllerPayload.Binding.Controller.FullName)
                    .ForContext("method", controllerPayload.Binding.Method.Name);

                message.Append(" {controller}.{method}");
                messageParams.Add(controllerPayload.Binding.Controller.FullName);
                messageParams.Add(controllerPayload.Binding.Method.Name);
            }

            if (IncludeMessageInfo)
            {
                message.Append(" on exchange {exchange}, queue {queue}, routingKey {routingKey}, replyTo {replyTo}, correlationId {correlationId}");
                messageParams.Add(messageContext.Exchange);
                messageParams.Add(messageContext.Queue);
                messageParams.Add(messageContext.RoutingKey);
                messageParams.Add(messageContext.Properties.ReplyTo);
                messageParams.Add(messageContext.Properties.CorrelationId);
            }

            if (IncludeMessageBody)
            {
                message.Append(" with body {body}");
                messageParams.Add(messageContext.RawBody != null ? Encoding.UTF8.GetString(messageContext.RawBody) : null);
            }

            contextLogger.Error(exception, message.ToString(), messageParams.ToArray());
        }

        /// <inheritdoc />
        public void QueueDeclare(string queueName, bool durable, bool passive)
        {
            if (passive)
                seriLogger.Write(BindingEventsLevel, "Tapeti: verifying durable queue {queueName}", queueName);
            else
                seriLogger.Write(BindingEventsLevel, "Tapeti: declaring {queueType} queue {queueName}", durable ? "durable" : "dynamic", queueName);
        }

        /// <inheritdoc />
        public void QueueExistsWarning(string queueName, IRabbitMQArguments? existingArguments, IRabbitMQArguments? arguments)
        {
            seriLogger.Warning("Tapeti: durable queue {queueName} exists with incompatible x-arguments ({existingArguments} vs. {arguments}) and will not be redeclared, queue will be consumed as-is",
                queueName,
                existingArguments,
                arguments);
        }

        /// <inheritdoc />
        public void QueueBind(string queueName, bool durable, string exchange, string routingKey)
        {
            seriLogger.Write(BindingEventsLevel, "Tapeti: binding {queueName} to exchange {exchange} with routing key {routingKey}",
                queueName,
                exchange,
                routingKey);
        }

        /// <inheritdoc />
        public void QueueUnbind(string queueName, string exchange, string routingKey)
        {
            seriLogger.Write(BindingEventsLevel, "Tapeti: removing binding for {queueName} to exchange {exchange} with routing key {routingKey}",
                queueName,
                exchange,
                routingKey);
        }

        /// <inheritdoc />
        public void ExchangeDeclare(string exchange)
        {
            seriLogger.Write(BindingEventsLevel, "Tapeti: declaring exchange {exchange}", exchange);
        }

        /// <inheritdoc />
        public void QueueObsolete(string queueName, bool deleted, uint messageCount)
        {
            if (deleted)
                seriLogger.Information("Tapeti: obsolete queue {queue} has been deleted", queueName);
            else
                seriLogger.Information("Tapeti: obsolete queue {queue} has been unbound but not yet deleted, {messageCount} messages remaining", queueName, messageCount);
        }


        /// <inheritdoc />
        public void ChannelCreated(ChannelCreatedContext context)
        {
            seriLogger.Write(ChannelEventsLevel, "Tapeti: channel #{channelNumber} on connection {connectionReference} of type {channelType} {createType}",
                context.ChannelNumber,
                context.ConnectionReference,
                context.ChannelType,
                context.IsRecreate ? "re-created" : "created");
        }


        /// <inheritdoc />
        public void ChannelShutdown(ChannelShutdownContext context)
        {
            seriLogger.Write(ChannelEventsLevel, "Tapeti: channel #{channelNumber} on connection {connectionReference} of type {channelType} shut down, code {replyCode}: {replyText}",
                context.ChannelNumber,
                context.ConnectionReference,
                context.ChannelType,
                context.ReplyCode,
                context.ReplyText);
        }
    }
}
