using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Tapeti.Config;
using Tapeti.Connection;
using Tapeti.Default;
using Tapeti.Exceptions;
using Tapeti.Helpers;

namespace Tapeti.Transport;


/// <summary>
/// Raised when an operation is attempted on a connection which has been reset.
/// </summary>
[PublicAPI]
public class ConnectionInvalidatedException : Exception
{
    /// <summary>
    /// The connection reference of the current connection.
    /// </summary>
    public long CurrentConnectionReference { get; }

    /// <summary>
    /// The expected connection reference.
    /// </summary>
    public long ExpectedConnectionReference { get; }

    /// <inheritdoc />
    public ConnectionInvalidatedException(long expectedConnectionReference, long currentConnectionReference)
        : base($"Connection reset, expected reference {expectedConnectionReference}, current reference {currentConnectionReference}")
    {
        ExpectedConnectionReference = expectedConnectionReference;
        CurrentConnectionReference = currentConnectionReference;
    }
}


/// <summary>
/// Concrete implementation of <see cref="ITapetiTransportFactory"/> for <see cref="TapetiTransport"/>.
/// </summary>
public class TapetiTransportFactory : ITapetiTransportFactory
{
    private readonly ILogger logger;


    /// <inheritdoc cref="TapetiTransportFactory" />
    public TapetiTransportFactory(ILogger logger)
    {
        this.logger = logger;
    }


    /// <inheritdoc />
    public ITapetiTransport Create(TapetiConnectionParams connectionParams)
    {
        return new TapetiTransport(logger, connectionParams);
    }
}


/// <summary>
/// Concrete implementation of <see cref="ITapetiTransport"/> using the RabbitMQ .NET Client.
/// </summary>
[PublicAPI]
public class TapetiTransport : ITapetiTransport
{
    /// <inheritdoc />
    public bool IsClosing { get; private set; }


    private const int ReconnectDelay = 5000;
    private const int MinimumConnectedReconnectDelay = 1000;

    private const int ChannelRecreateDelay = 5000;
    private const int MinimumChannelRecreateDelay = 1000;


    private readonly ILogger logger;
    private readonly TapetiConnectionParams connectionParams;
    private readonly List<ITapetiTransportObserver> observers = [];

    internal readonly RabbitMQManagementAPI ManagementAPI;

    // These fields must be locked using connectionLock
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private ConnectionFactory? connectionFactory;
    private long connectionReference;
    private RabbitMQ.Client.IConnection? connection;
    private bool isReconnect;
    private DateTime connectedDateTime;
    private TapetiTransportState state;


    /// <inheritdoc cref="TapetiTransport"/>
    public TapetiTransport(ILogger logger, TapetiConnectionParams connectionParams)
    {
        this.logger = logger;
        this.connectionParams = connectionParams;

        ManagementAPI = new RabbitMQManagementAPI(connectionParams);
        state = new TapetiTransportState();
    }


    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return default;
    }


    /// <inheritdoc />
    public async ValueTask Open()
    {
        await WithConnection((_, _) => ValueTask.CompletedTask);
    }


    /// <inheritdoc />
    public async ValueTask Close()
    {
        RabbitMQ.Client.IConnection? capturedConnection;

        await connectionLock.WaitAsync();
        try
        {
            IsClosing = true;
            capturedConnection = connection;
            connection = null;
        }
        finally
        {
            connectionLock.Release();
        }

        // ReSharper disable once InvertIf
        if (capturedConnection != null)
        {
            try
            {
                await capturedConnection.CloseAsync();
            }
            finally
            {
                capturedConnection.Dispose();
            }
        }
    }


    /// <inheritdoc />
    public async Task<ITapetiTransportChannel> CreateChannel(TapetiChannelOptions options)
    {
        ITapetiTransportChannel channel = null!;

        await WithConnection(async (capturedConnection, capturedConnectionReference) =>
        {
            var clientChannel = await capturedConnection.CreateChannelAsync(new CreateChannelOptions(options.PublisherConfirmationsEnabled, false, null, null));
            channel = new TapetiTransportChannel(this, logger, options, clientChannel, state, capturedConnectionReference);
        });

        return channel;

        // TODO move to channel recreate
        //if (sameConnection && (DateTime.UtcNow - channelReference!.Value.CreatedDateTime).TotalMilliseconds <= MinimumChannelRecreateDelay)
        //  Thread.Sleep(ChannelRecreateDelay);
    }


    /// <inheritdoc />
    public void AttachObserver(ITapetiTransportObserver observer)
    {
        observers.Add(observer);
    }


    internal long GetConnectionReference()
    {
        return Interlocked.Read(ref connectionReference);
    }


    private async Task WithConnection(Func<RabbitMQ.Client.IConnection, long, ValueTask> callback)
    {
        await connectionLock.WaitAsync();
        try
        {
            if (connection is not { IsOpen: true })
            {
                if (connection is not null)
                {
                    try
                    {
                        await connection.CloseAsync();
                    }
                    catch (AlreadyClosedException)
                    {
                    }
                    finally
                    {
                        await connection.DisposeAsync();
                    }
                }

                connection = await Connect();
            }

            var capturedConnectionReference = Interlocked.Read(ref connectionReference);
            await callback(connection, capturedConnectionReference);
        }
        finally
        {
            connectionLock.Release();
        }
    }


    /// <remarks>
    /// Must be called within the connectionLock!
    /// </remarks>
    private async Task<RabbitMQ.Client.IConnection> Connect()
    {
        // If the Disconnect quickly follows the Connect (when an error occurs that is reported back by RabbitMQ
        // not related to the connection), wait for a bit to avoid spamming the connection
        if ((DateTime.UtcNow - connectedDateTime).TotalMilliseconds <= MinimumConnectedReconnectDelay)
            Thread.Sleep(ReconnectDelay);

        RabbitMQ.Client.IConnection newConnection;
        while (true)
        {
            try
            {
                connectionFactory ??= CreateConnectionFactory();
                var capturedConnectionReference = Interlocked.Increment(ref connectionReference);

                newConnection = await connectionFactory.CreateConnectionAsync();
                newConnection.ConnectionShutdownAsync += (_, e) =>
                {
                    if (capturedConnectionReference != GetConnectionReference())
                        return Task.CompletedTask;

                    NotifyDisconnected(new DisconnectedEventArgs
                    {
                        ReplyCode = e.ReplyCode,
                        ReplyText = e.ReplyText
                    });

                    logger.Disconnect(new DisconnectContext
                    {
                        ConnectionParams = connectionParams,
                        ReplyCode = e.ReplyCode,
                        ReplyText = e.ReplyText
                    });

                    return Task.CompletedTask;
                };

                connectedDateTime = DateTime.UtcNow;

                var connectedEventArgs = new ConnectedEventArgs
                {
                    ConnectionParams = connectionParams,
                    LocalPort = newConnection.LocalPort
                };

                if (isReconnect)
                    NotifyReconnected(connectedEventArgs);
                else
                    NotifyConnected(connectedEventArgs);

                logger.ConnectSuccess(new ConnectSuccessContext
                {
                    ConnectionParams = connectionParams,
                    IsReconnect = isReconnect,
                    LocalPort = newConnection.LocalPort
                });
                isReconnect = true;

                break;
            }
            catch (BrokerUnreachableException e)
            {
                logger.ConnectFailed(new ConnectFailedContext
                {
                    ConnectionParams = connectionParams,
                    IsReconnect = isReconnect,
                    Exception = e
                });
                Thread.Sleep(ReconnectDelay);
            }
        }

        return newConnection;
    }


    /// <summary>
    /// Creates a <see cref="ConnectionFactory"/> from the <see cref="TapetiConnectionParams"/> by default,
    /// can be overridden for more scenarios not supported by Tapeti by default.
    /// </summary>
    protected virtual ConnectionFactory CreateConnectionFactory()
    {
        var factory = new ConnectionFactory
        {
            HostName = connectionParams.HostName,
            Port = connectionParams.Port,
            VirtualHost = connectionParams.VirtualHost,
            UserName = connectionParams.Username,
            Password = connectionParams.Password,
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false,
            RequestedHeartbeat = TimeSpan.FromSeconds(30)
        };

        if (connectionParams.ConsumerDispatchConcurrency > 0)
            factory.ConsumerDispatchConcurrency = connectionParams.ConsumerDispatchConcurrency;

        // ReSharper disable once InvertIf
        if (connectionParams.ClientProperties != null)
            foreach (var pair in connectionParams.ClientProperties)
            {
                if (factory.ClientProperties.ContainsKey(pair.Key))
                    factory.ClientProperties[pair.Key] = Encoding.UTF8.GetBytes(pair.Value);
                else
                    factory.ClientProperties.Add(pair.Key, Encoding.UTF8.GetBytes(pair.Value));
            }

        return factory;
    }


    private void NotifyConnected(ConnectedEventArgs e)
    {
        foreach (var observer in observers)
            observer.Connected(e);
    }


    private void NotifyReconnected(ConnectedEventArgs e)
    {
        foreach (var observer in observers)
            observer.Reconnected(e);
    }


    private void NotifyDisconnected(DisconnectedEventArgs e)
    {
        foreach (var observer in observers)
            observer.Disconnected(e);
    }


    private class TapetiTransportChannel : ITapetiTransportChannel
    {
        private const int MandatoryReturnTimeout = 300000;

        public long ConnectionReference { get; }
        public long ChannelNumber { get; }

        private readonly TapetiTransport owner;
        private readonly ILogger logger;
        private readonly TapetiChannelOptions options;
        private readonly IChannel channel;
        private readonly TapetiTransportState state;

        private bool shutdownCalled;
        private readonly List<ITapetiTransportChannelObserver> observers = [];

        private ulong lastDeliveryTag;

        // These fields must be locked using confirmLock, since the callbacks for BasicAck/BasicReturn can run in a different thread
        private readonly object confirmLock = new();
        private readonly Dictionary<ulong, ConfirmMessageInfo> confirmMessages = new();
        private readonly Dictionary<string, ReturnInfo> returnRoutingKeys = new();



        public TapetiTransportChannel(TapetiTransport owner, ILogger logger, TapetiChannelOptions options, IChannel channel, TapetiTransportState state, long connectionReference)
        {
            this.owner = owner;
            this.logger = logger;
            this.options = options;
            this.channel = channel;
            this.state = state;

            ConnectionReference = connectionReference;
            ChannelNumber = channel.ChannelNumber;

            channel.ChannelShutdownAsync += async (_, e) =>
            {
                await NotifyShutdown(new ChannelShutdownEventArgs
                {
                    IsClosing = owner.IsClosing,
                    Initiator = e.Initiator,
                    ReplyCode = e.ReplyCode,
                    ReplyText = e.ReplyText
                });
            };


            channel.BasicReturnAsync += HandleBasicReturn;
            channel.BasicAcksAsync += HandleBasicAck;
            channel.BasicNacksAsync += HandleBasicNack;
        }


        public void AttachObserver(ITapetiTransportChannelObserver observer)
        {
            observers.Add(observer);
        }

        public async Task<ITapetiTransportConsumer?> Consume(string queueName, IConsumer consumer, CancellationToken cancellationToken)
        {
            await ValidateConnectionReference();

            if (state.IsQueueDeleted(queueName))
                return null;

            if (string.IsNullOrEmpty(queueName))
                throw new ArgumentNullException(nameof(queueName));


            if (cancellationToken.IsCancellationRequested)
                return null;

            var transportConsumer = new TapetiTransportConsumer(channel, consumer, state.MessageHandlerTracker);
            var consumerTag = await channel.BasicConsumeAsync(queueName, false, transportConsumer, cancellationToken: CancellationToken.None);

            return new TransportConsumer(channel, consumerTag);
        }

        public async Task Publish(byte[] body, IMessageProperties properties, string? exchange, string routingKey, bool mandatory)
        {
            await ValidateConnectionReference();

            if (string.IsNullOrEmpty(routingKey))
                throw new ArgumentNullException(nameof(routingKey));


            Task<int>? publishResultTask = null;
            var messageInfo = new ConfirmMessageInfo
            {
                ReturnKey = GetReturnKey(exchange ?? string.Empty, routingKey),
                CompletionSource = new TaskCompletionSource<int>()
            };

            // TODO ported from WithRetryableChannel, should be improved
            while (true)
            {
                try
                {
                    if (exchange != null)
                        await DeclareExchange(exchange);

                    // The delivery tag is lost after a reconnect, register under the new tag
                    if (options.PublisherConfirmationsEnabled)
                    {
                        lastDeliveryTag++;

                        Monitor.Enter(confirmLock);
                        try
                        {
                            confirmMessages.Add(lastDeliveryTag, messageInfo);
                        }
                        finally
                        {
                            Monitor.Exit(confirmLock);
                        }

                        publishResultTask = messageInfo.CompletionSource.Task;
                    }
                    else
                        mandatory = false;

                    try
                    {
                        await channel.BasicPublishAsync(exchange ?? string.Empty, routingKey, mandatory, properties.ToBasicProperties(), body);
                    }
                    catch
                    {
                        messageInfo.CompletionSource.SetCanceled();
                        publishResultTask = null;

                        throw;
                    }

                    break;
                }
                catch (AlreadyClosedException)
                {
                }
            }


            if (publishResultTask == null)
                return;

            var delayCancellationTokenSource = new CancellationTokenSource();
            var signalledTask = await Task.WhenAny(
                publishResultTask,
                Task.Delay(MandatoryReturnTimeout, delayCancellationTokenSource.Token)).ConfigureAwait(false);

            if (signalledTask != publishResultTask)
                throw new TimeoutException(
                    $"Timeout while waiting for basic.return for message with exchange '{exchange}' and routing key '{routingKey}'");

            await delayCancellationTokenSource.CancelAsync();

            if (publishResultTask.IsCanceled)
                throw new NackException(
                    $"Mandatory message with with exchange '{exchange}' and routing key '{routingKey}' was nacked");

            var replyCode = publishResultTask.Result;

            switch (replyCode)
            {
                // There is no RabbitMQ.Client.Framing.Constants value for this "No route" reply code
                // at the time of writing...
                case 312:
                    throw new NoRouteException(
                        $"Mandatory message with exchange '{exchange}' and routing key '{routingKey}' does not have a route");

                case > 0:
                    throw new NoRouteException(
                        $"Mandatory message with exchange '{exchange}' and routing key '{routingKey}' could not be delivered, reply code: {replyCode}");
            }
        }

        public async Task DurableQueueDeclare(string queueName, IEnumerable<QueueBinding> bindings, IRabbitMQArguments? arguments, CancellationToken cancellationToken)
        {
            await ValidateConnectionReference();

            var declareRequired = await GetDurableQueueDeclareRequired(queueName, arguments).ConfigureAwait(false);

            var existingBindings = (await owner.ManagementAPI.GetQueueBindings(queueName).ConfigureAwait(false)).ToList();
            var currentBindings = bindings.ToList();
            var bindingLogger = logger as IBindingLogger;

            if (cancellationToken.IsCancellationRequested)
                return;

            if (declareRequired)
            {
                bindingLogger?.QueueDeclare(queueName, true, false);
                await channel.QueueDeclareAsync(queueName, true, false, false, GetDeclareArguments(arguments), cancellationToken: cancellationToken);
            }

            foreach (var binding in currentBindings.Except(existingBindings))
            {
                await DeclareExchange(binding.Exchange);
                bindingLogger?.QueueBind(queueName, true, binding.Exchange, binding.RoutingKey);
                await channel.QueueBindAsync(queueName, binding.Exchange, binding.RoutingKey, cancellationToken: cancellationToken);
            }

            foreach (var deletedBinding in existingBindings.Except(currentBindings))
            {
                bindingLogger?.QueueUnbind(queueName, deletedBinding.Exchange, deletedBinding.RoutingKey);
                await channel.QueueUnbindAsync(queueName, deletedBinding.Exchange, deletedBinding.RoutingKey, cancellationToken: cancellationToken);
            }
        }

        public async Task DurableQueueVerify(string queueName, IRabbitMQArguments? arguments, CancellationToken cancellationToken)
        {
            await ValidateConnectionReference();
            throw new NotImplementedException();
        }

        public async Task DurableQueueDelete(string queueName, bool onlyIfEmpty, CancellationToken cancellationToken)
        {
            await ValidateConnectionReference();
            throw new NotImplementedException();
        }

        public async Task<string> DynamicQueueDeclare(string? queuePrefix, IRabbitMQArguments? arguments, CancellationToken cancellationToken)
        {
            await ValidateConnectionReference();

            string? queueName;
            var bindingLogger = logger as IBindingLogger;

            if (!string.IsNullOrEmpty(queuePrefix))
            {
                queueName = queuePrefix + "." + Guid.NewGuid().ToString("N");
                bindingLogger?.QueueDeclare(queueName, false, false);
                await channel.QueueDeclareAsync(queueName, arguments: GetDeclareArguments(arguments), cancellationToken: cancellationToken);
            }
            else
            {
                queueName = (await channel.QueueDeclareAsync(arguments: GetDeclareArguments(arguments), cancellationToken: cancellationToken)).QueueName;
                bindingLogger?.QueueDeclare(queueName, false, false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (queueName == null)
                throw new InvalidOperationException("Failed to declare dynamic queue");

            return queueName;
        }

        public async Task DynamicQueueBind(string queueName, QueueBinding binding, CancellationToken cancellationToken)
        {
            await ValidateConnectionReference();

            if (cancellationToken.IsCancellationRequested)
                return;

            await DeclareExchange(binding.Exchange);
            (logger as IBindingLogger)?.QueueBind(queueName, false, binding.Exchange, binding.RoutingKey);
            await channel.QueueBindAsync(queueName, binding.Exchange, binding.RoutingKey, cancellationToken: cancellationToken);
        }


        private async ValueTask ValidateConnectionReference()
        {
            var currentReference = owner.GetConnectionReference();
            if (currentReference == ConnectionReference)
                return;

            await NotifyShutdown(new ChannelShutdownEventArgs
            {
                IsClosing = owner.IsClosing,
                Initiator = ShutdownInitiator.Library,
                ReplyCode = null,
                ReplyText = null,
            });

            throw new ConnectionInvalidatedException(ConnectionReference, currentReference);
        }


        private async ValueTask NotifyShutdown(ChannelShutdownEventArgs e)
        {
            if (shutdownCalled)
                return;

            shutdownCalled = true;
            foreach (var observer in observers)
                await observer.OnShutdown(e);
        }


        private async ValueTask DeclareExchange(string exchange)
        {
            if (state.IsExchangeDeclared(exchange))
                return;

            (logger as IBindingLogger)?.ExchangeDeclare(exchange);
            await channel.ExchangeDeclareAsync(exchange, "topic", true);
            state.SetExchangeDeclared(exchange);
        }


        private async Task<bool> GetDurableQueueDeclareRequired(string queueName, IRabbitMQArguments? arguments)
        {
            var existingQueue = await owner.ManagementAPI.GetQueueInfo(queueName).ConfigureAwait(false);
            if (existingQueue == null)
                return true;

            if (!existingQueue.Durable || existingQueue.AutoDelete || existingQueue.Exclusive)
                throw new InvalidOperationException($"Durable queue {queueName} already exists with incompatible parameters, durable = {existingQueue.Durable} (expected True), autoDelete = {existingQueue.AutoDelete} (expected False), exclusive = {existingQueue.Exclusive} (expected False)");

            var existingArguments = ConvertJsonArguments(existingQueue.Arguments);
            if (existingArguments.NullSafeSameValues(arguments))
                return true;

            (logger as IBindingLogger)?.QueueExistsWarning(queueName, existingArguments, arguments);
            return false;
        }


        private static RabbitMQArguments? ConvertJsonArguments(IReadOnlyDictionary<string, JValue>? arguments)
        {
            if (arguments == null)
                return null;

            var result = new RabbitMQArguments();
            foreach (var pair in arguments)
            {
                // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault - by design
                object value = pair.Value.Type switch
                {
                    JTokenType.Integer => pair.Value.Value<int>(),
                    JTokenType.Float => pair.Value.Value<double>(),
                    JTokenType.String => pair.Value.Value<string>() ?? string.Empty,
                    JTokenType.Boolean => pair.Value.Value<bool>(),
                    _ => throw new ArgumentOutOfRangeException(nameof(arguments))
                };

                result.Add(pair.Key, value);
            }

            return result;
        }


        private static Dictionary<string, object?>? GetDeclareArguments(IRabbitMQArguments? arguments)
        {
            return arguments == null || arguments.Count == 0
                ? null
                : arguments.ToDictionary(p => p.Key, object? (p) => p.Value);
        }


        private static string GetReturnKey(string exchange, string routingKey)
        {
            return exchange + ':' + routingKey;
        }


        private Task HandleBasicReturn(object? sender, BasicReturnEventArgs e)
        {
            /*
             * "If the message is also published as mandatory, the basic.return is sent to the client before basic.ack."
             * - https://www.rabbitmq.com/confirms.html
             *
             * Because there is no delivery tag included in the basic.return message. This solution is modeled after
             * user OhJeez' answer on StackOverflow:
             *
             * "Since all messages with the same routing key are routed the same way. I assumed that once I get a
             *  basic.return about a specific routing key, all messages with this routing key can be considered undelivered"
             * https://stackoverflow.com/questions/21336659/how-to-tell-which-amqp-message-was-not-routed-from-basic-return-response
             */
            var key = GetReturnKey(e.Exchange, e.RoutingKey);

            if (!returnRoutingKeys.TryGetValue(key, out var returnInfo))
            {
                returnInfo = new ReturnInfo
                {
                    RefCount = 0,
                    FirstReplyCode = e.ReplyCode
                };

                returnRoutingKeys.Add(key, returnInfo);
            }

            returnInfo.RefCount++;
            return Task.CompletedTask;
        }


        private Task HandleBasicAck(object? sender, BasicAckEventArgs e)
        {
            Monitor.Enter(confirmLock);
            try
            {
                foreach (var deliveryTag in GetDeliveryTags(e))
                {
                    if (!confirmMessages.TryGetValue(deliveryTag, out var messageInfo))
                        continue;

                    if (returnRoutingKeys.TryGetValue(messageInfo.ReturnKey, out var returnInfo))
                    {
                        messageInfo.CompletionSource.SetResult(returnInfo.FirstReplyCode);

                        returnInfo.RefCount--;
                        if (returnInfo.RefCount == 0)
                            returnRoutingKeys.Remove(messageInfo.ReturnKey);
                    }
                    else
                        messageInfo.CompletionSource.SetResult(0);

                    confirmMessages.Remove(deliveryTag);
                }
            }
            finally
            {
                Monitor.Exit(confirmLock);
            }

            return Task.CompletedTask;
        }


        private Task HandleBasicNack(object? sender, BasicNackEventArgs e)
        {
            Monitor.Enter(confirmLock);
            try
            {
                foreach (var deliveryTag in GetDeliveryTags(e))
                {
                    if (!confirmMessages.TryGetValue(deliveryTag, out var messageInfo))
                        continue;

                    messageInfo.CompletionSource.SetCanceled();
                    confirmMessages.Remove(e.DeliveryTag);
                }
            }
            finally
            {
                Monitor.Exit(confirmLock);
            }

            return Task.CompletedTask;
        }


        private ulong[] GetDeliveryTags(BasicAckEventArgs e)
        {
            return e.Multiple
                ? confirmMessages.Keys.Where(tag => tag <= e.DeliveryTag).ToArray()
                : [e.DeliveryTag];
        }


        private ulong[] GetDeliveryTags(BasicNackEventArgs e)
        {
            return e.Multiple
                ? confirmMessages.Keys.Where(tag => tag <= e.DeliveryTag).ToArray()
                : [e.DeliveryTag];
        }
    }


    private class ConfirmMessageInfo
    {
        public required string ReturnKey { get; init; }
        public required TaskCompletionSource<int> CompletionSource { get; init; }
    }


    private class ReturnInfo
    {
        public required uint RefCount { get; set; }
        public required int FirstReplyCode { get; init; }
    }


    private class TransportConsumer : ITapetiTransportConsumer
    {
        private readonly IChannel channel;
        private readonly string consumerTag;


        public TransportConsumer(IChannel channel, string consumerTag)
        {
            this.channel = channel;
            this.consumerTag = consumerTag;
        }


        public async Task Cancel()
        {
            if (channel.IsClosed)
                return;

            await channel.BasicCancelAsync(consumerTag);
        }
    }
}
