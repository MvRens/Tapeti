using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Tapeti.Config;
using Tapeti.Connection;
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

    internal readonly RabbitMQManagementAPI ManagementAPI;

    // These fields must be locked using connectionLock
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private ConnectionFactory? connectionFactory;
    private long connectionReference;
    private RabbitMQ.Client.IConnection? connection;
    private bool isReconnect;
    private DateTime connectedDateTime;
    private IChannel? connectionMonitorChannel;


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
    public ValueTask Open()
    {
        throw new NotImplementedException();
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
        /*
        var sameConnection = channelReference is not null && channelReference.Value.ConnectionReference == Interlocked.Read(ref connectionReference);
        if (sameConnection && channelReference!.Value.Channel.IsOpen)
            return channelReference.Value;
        */

        ITapetiTransportChannel channel = null!;

        await WithConnection(async (capturedConnection, capturedConnectionReference) =>
        {
            // TODO use native publisher confirm tracking?
            var clientChannel = await capturedConnection.CreateChannelAsync(new CreateChannelOptions(options.PublisherConfirmationsEnabled, false, null, null));

            //onInitChannel?.Invoke(clientChannel);
            channel = new TapetiTransportChannel(this, logger, clientChannel, capturedConnectionReference);
        });

        return channel;

        // TODO move to channel recreate
        //if (sameConnection && (DateTime.UtcNow - channelReference!.Value.CreatedDateTime).TotalMilliseconds <= MinimumChannelRecreateDelay)
        //  Thread.Sleep(ChannelRecreateDelay);
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
            long capturedConnectionReference;

            if (connection is not { IsOpen: true })
            {
                try
                {
                    if (connection is not null)
                        await connection.CloseAsync();
                }
                catch (AlreadyClosedException)
                {
                }
                finally
                {
                    connection?.Dispose();
                }

                // TODO disconnected event
                // TODO logger.Connect(new ConnectContext(connectionParams, isReconnect));

                capturedConnectionReference = Interlocked.Increment(ref connectionReference);
                connection = await Connect();
            }
            else
                capturedConnectionReference = Interlocked.Read(ref connectionReference);

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
                newConnection = await connectionFactory.CreateConnectionAsync();
                connectionMonitorChannel = await newConnection.CreateChannelAsync();
                if (connectionMonitorChannel == null)
                    throw new BrokerUnreachableException(new AggregateException());

                var capturedConnectionMonitorChannel = connectionMonitorChannel;

                connectionMonitorChannel.ChannelShutdownAsync += async (_, e) =>
                {
                    bool capturedIsClosing;

                    await connectionLock.WaitAsync();
                    try
                    {
                        if (connectionMonitorChannel == null || connectionMonitorChannel != capturedConnectionMonitorChannel)
                            return;

                        capturedConnectionMonitorChannel = null;
                        capturedIsClosing = IsClosing;
                    }
                    finally
                    {
                        connectionLock.Release();
                    }

                    // TODO ConnectionEventListener?.Disconnected(new DisconnectedEventArgs(e.ReplyCode, e.ReplyText));
                    // TODO logger.Disconnect(new DisconnectContext(connectionParams, e.ReplyCode, e.ReplyText));

                    // Reconnect if the disconnect was unexpected
                    // TODO if (!capturedIsClosing)
                        // Note: I'm not too happy with this design, but letting the Client handle the reconnect is
                        // effectively how it was done before TapetiClientConnection was split off and since it
                        // manages the channels it is the best I could come up with for now.
                        // TODO OnQueueReconnect?.Invoke();
                };

                connectedDateTime = DateTime.UtcNow;

                /* TODO
                var connectedEventArgs = new ConnectedEventArgs(connectionParams, newConnection.LocalPort);

                if (isReconnect)
                    ConnectionEventListener?.Reconnected(connectedEventArgs);
                else
                    ConnectionEventListener?.Connected(connectedEventArgs);

                logger.ConnectSuccess(new ConnectContext(connectionParams, isReconnect, newConnection.LocalPort));
                */
                isReconnect = true;

                break;
            }
            catch (BrokerUnreachableException e)
            {
                // TODO logger.ConnectFailed(new ConnectContext(connectionParams, isReconnect, exception: e));
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



    private class TapetiTransportChannel : ITapetiTransportChannel
    {
        private readonly TapetiTransport owner;
        private readonly ILogger logger;
        private readonly IChannel channel;
        private readonly long connectionReference;

        private bool shutdownCalled;
        private readonly List<ITapetiTransportChannelObserver> observers = [];

        private readonly HashSet<string> declaredExchanges = new();


        public TapetiTransportChannel(TapetiTransport owner, ILogger logger, IChannel channel, long connectionReference)
        {
            this.owner = owner;
            this.logger = logger;
            this.channel = channel;
            this.connectionReference = connectionReference;

            channel.ChannelShutdownAsync += async (_, e) =>
            {
                // TODO parameters
                await Shutdown();
            };
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
            throw new NotImplementedException();
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

            await Shutdown();
            throw new ConnectionInvalidatedException(connectionReference, currentReference);
        }


        private async ValueTask Shutdown()
        {
            if (shutdownCalled)
                return;

            shutdownCalled = true;
            foreach (var observer in observers)
                await observer.OnShutdown();
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
    }
}
