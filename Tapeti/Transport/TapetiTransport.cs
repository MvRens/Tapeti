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
    private TapetiChannel? connectionMonitorChannel;


    /// <inheritdoc cref="TapetiTransport"/>
    public TapetiTransport(ILogger logger, TapetiConnectionParams connectionParams)
    {
        this.logger = logger;
        this.connectionParams = connectionParams;

        ManagementAPI = new RabbitMQManagementAPI(connectionParams);
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
            channel = new TapetiTransportChannel(this, logger, options, clientChannel, capturedConnectionReference);
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

                if (connectionMonitorChannel is null)
                {
                    // Prevent deadlocks by providing this channel with a special transport interface
                    // which only works for the current connection
                    var immediateTransport = new SingleConnectionTransport(this, newConnection, capturedConnectionReference);

                    connectionMonitorChannel = new TapetiChannel(immediateTransport, new TapetiChannelOptions
                    {
                        PublisherConfirmationsEnabled = false
                    });

                    connectionMonitorChannel.AttachObserver(new ConnectionMonitorChannelObserver(this, capturedConnectionReference));
                    await connectionMonitorChannel.Open();

                    immediateTransport.SetConnected();
                }

                connectedDateTime = DateTime.UtcNow;

                var connectedEventArgs = new ConnectedEventArgs(connectionParams, newConnection.LocalPort);
                if (isReconnect)
                    NotifyReconnected(connectedEventArgs);
                else
                    NotifyConnected(connectedEventArgs);

                logger.ConnectSuccess(new ConnectContext
                {
                    ConnectionParams = connectionParams,
                    IsReconnect = isReconnect,
                    LocalPort = newConnection.LocalPort,
                    Exception = null
                });
                isReconnect = true;

                break;
            }
            catch (BrokerUnreachableException e)
            {
                logger.ConnectFailed(new ConnectContext
                {
                    ConnectionParams = connectionParams,
                    IsReconnect = isReconnect,
                    LocalPort = 0,
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


    private void ConnectionMonitorChannelShutdown(ChannelShutdownEventArgs e, long channelConnectionReference)
    {
        if (channelConnectionReference != GetConnectionReference())
            return;

        var replyCode = e.ReplyCode.GetValueOrDefault();
        var replyText = e.ReplyText ?? string.Empty;

        NotifyDisconnected(new DisconnectedEventArgs(replyCode, replyText));
        logger.Disconnect(new DisconnectContext
        {
            ConnectionParams = connectionParams,
            ReplyCode = replyCode,
            ReplyText = replyText
        });
    }


    private ValueTask ConnectionMonitorChannelRecreated()
    {
        return default;
    }


    private class TapetiTransportChannel : ITapetiTransportChannel
    {
        private const int MandatoryReturnTimeout = 300000;


        private readonly TapetiTransport owner;
        private readonly ILogger logger;
        private readonly TapetiChannelOptions options;
        private readonly IChannel channel;
        private readonly long connectionReference;

        private bool shutdownCalled;
        private readonly List<ITapetiTransportChannelObserver> observers = [];

        private readonly HashSet<string> declaredExchanges = [];
        private readonly HashSet<string> deletedQueues = [];

        private ulong lastDeliveryTag;

        // These fields must be locked using confirmLock, since the callbacks for BasicAck/BasicReturn can run in a different thread
        private readonly object confirmLock = new();
        private readonly Dictionary<ulong, ConfirmMessageInfo> confirmMessages = new();
        private readonly Dictionary<string, ReturnInfo> returnRoutingKeys = new();



        public TapetiTransportChannel(TapetiTransport owner, ILogger logger, TapetiChannelOptions options, IChannel channel, long connectionReference)
        {
            this.owner = owner;
            this.logger = logger;
            this.options = options;
            this.channel = channel;
            this.connectionReference = connectionReference;

            channel.ChannelShutdownAsync += async (_, e) =>
            {
                await NotifyShutdown(new ChannelShutdownEventArgs
                {
                    IsClosing = owner.IsClosing,
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }


        private async ValueTask ValidateConnectionReference()
        {
            var currentReference = owner.GetConnectionReference();
            if (currentReference == connectionReference)
                return;

            await NotifyShutdown(new ChannelShutdownEventArgs
            {
                IsClosing = owner.IsClosing,
                ReplyCode = null,
                ReplyText = null
            });

            throw new ConnectionInvalidatedException(connectionReference, currentReference);
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
            if (declaredExchanges.Contains(exchange))
                return;

            (logger as IBindingLogger)?.ExchangeDeclare(exchange);
            await channel.ExchangeDeclareAsync(exchange, "topic", true);
            declaredExchanges.Add(exchange);
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

    private class ConnectContext : IConnectSuccessContext, IConnectFailedContext
    {
        public required TapetiConnectionParams ConnectionParams { get; init; }
        public required bool IsReconnect { get; init; }
        public required int LocalPort { get; init; }
        public required Exception? Exception { get; init; }
    }

    private class DisconnectContext : IDisconnectContext
    {
        public required TapetiConnectionParams ConnectionParams { get; init; }
        public required ushort ReplyCode { get; init; }
        public required string ReplyText { get; init; }
    }


    private class SingleConnectionTransport : ITapetiTransport
    {
        public bool IsClosing => owner.IsClosing;


        private readonly TapetiTransport owner;
        private readonly RabbitMQ.Client.IConnection connection;
        private readonly long connectionReference;

        private bool connected;


        public SingleConnectionTransport(TapetiTransport owner, RabbitMQ.Client.IConnection connection, long connectionReference)
        {
            this.owner = owner;
            this.connection = connection;
            this.connectionReference = connectionReference;
        }


        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return default;
        }


        public ValueTask Open()
        {
            return default;
        }

        public ValueTask Close()
        {
            return default;
        }


        public void SetConnected()
        {
            connected = true;
        }


        public async Task<ITapetiTransportChannel> CreateChannel(TapetiChannelOptions options)
        {
            // Initially the channel is opened within the lock, and we should simply create the channel on the
            // provided connection. Once the initial connection is established however, go through the normal route
            // to trigger a reconnect if required.
            if (connected)
                return await owner.CreateChannel(options);

            if (!connection.IsOpen || owner.GetConnectionReference() != connectionReference)
                throw new AlreadyClosedException(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "Cannot create a channel on an already closed connection."));

            var clientChannel = await connection.CreateChannelAsync(new CreateChannelOptions(options.PublisherConfirmationsEnabled, false, null, null));
            return new TapetiTransportChannel(owner, owner.logger, options, clientChannel, connectionReference);
        }


        public void AttachObserver(ITapetiTransportObserver observer)
        {
            owner.AttachObserver(observer);
        }
    }


    private class ConnectionMonitorChannelObserver : ITapetiChannelObserver
    {
        private readonly TapetiTransport owner;
        private long connectionReference;


        public ConnectionMonitorChannelObserver(TapetiTransport owner, long connectionReference)
        {
            this.owner = owner;
            this.connectionReference = connectionReference;
        }


        public ValueTask OnShutdown(ChannelShutdownEventArgs e)
        {
            owner.ConnectionMonitorChannelShutdown(e, connectionReference);
            return default;
        }


        public ValueTask OnRecreated(ITapetiTransportChannel newChannel)
        {
            connectionReference = owner.GetConnectionReference();
            return owner.ConnectionMonitorChannelRecreated();
        }
    }
}
