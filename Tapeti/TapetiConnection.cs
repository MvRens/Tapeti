using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Connection;
using Tapeti.Transport;

namespace Tapeti;

/// <summary>
/// Creates a connection to RabbitMQ based on the provided Tapeti config.
/// </summary>
public class TapetiConnection : IConnection
{
    /// <summary>
    /// Specifies the hostname and credentials to use when connecting to RabbitMQ.
    /// Defaults to guest on localhost.
    /// </summary>
    /// <remarks>
    /// This property must be set before first subscribing or publishing, otherwise it
    /// will use the default connection parameters.
    /// </remarks>
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global - backwards compatibility
    public TapetiConnectionParams? Params { get; set; }

    /// <inheritdoc />
    public event ConnectedEventHandler? Connected;

    /// <inheritdoc />
    public event DisconnectedEventHandler? Disconnected;

    /// <inheritdoc />
    public event ConnectedEventHandler? Reconnected;


    private readonly ITapetiConfig config;
    private readonly ITapetiTransportFactory transportFactory;
    private readonly ILogger logger;

    private readonly object initializedConnectionLock = new();
    private InitializedConnection? initializedConnection;

    //private List<TapetiChannel> dedicatedChannels = [];
    private TapetiSubscriber? subscriber;

    private bool disposed;


    /// <summary>
    /// Creates a new instance of a TapetiConnection and registers a default IPublisher
    /// in the IoC container as provided in the config.
    /// </summary>
    /// <param name="config"></param>
    public TapetiConnection(ITapetiConfig config)
    {
        this.config = config;
        (config.DependencyResolver as IDependencyContainer)?.RegisterDefault(GetPublisher);

        transportFactory = config.DependencyResolver.Resolve<ITapetiTransportFactory>();
        logger = config.DependencyResolver.Resolve<ILogger>();
    }


    /// <inheritdoc />
    public async Task<ISubscriber> Subscribe(bool startConsuming = true)
    {
        if (subscriber == null)
        {
            subscriber = new TapetiSubscriber(channelType =>
                channelType switch
                {
                    TapetiSubscriberChannelType.Default => EnsureInitialized().DefaultConsumeChannel,
                    TapetiSubscriberChannelType.Dedicated => EnsureInitialized().CreateDedicatedChannel(),
                    _ => throw new ArgumentOutOfRangeException(nameof(channelType), channelType, null)
                },
                config);
            await subscriber.ApplyBindings().ConfigureAwait(false);
        }

        if (startConsuming)
            await subscriber.Resume().ConfigureAwait(false);

        return subscriber;
    }


    /// <inheritdoc />
    public ISubscriber SubscribeSync(bool startConsuming = true)
    {
        return Subscribe(startConsuming).GetAwaiter().GetResult();
    }


    /// <inheritdoc />
    public Task Unsubscribe()
    {
        return subscriber?.Stop() ?? Task.CompletedTask;
    }


    /// <inheritdoc />
    public IPublisher GetPublisher()
    {
        return new TapetiPublisher(config, () => EnsureInitialized().DefaultPublishChannel);
    }

    /// <inheritdoc />
    public ValueTask Open()
    {
        return EnsureInitialized().Transport.Open();
    }


    /// <inheritdoc />
    public async Task Close()
    {
        InitializedConnection? capturedInitializedConnection;

        lock (initializedConnectionLock)
        {
            capturedInitializedConnection = initializedConnection;
            initializedConnection = null;
        }

        if (capturedInitializedConnection is not null)
            await capturedInitializedConnection.Transport.Close();
    }


    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (disposed)
            return;

        var disposeAsyncTask = DisposeAsync();
        if (!disposeAsyncTask.IsCompleted)
            disposeAsyncTask.AsTask().GetAwaiter().GetResult();
    }


    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (disposed)
            return;

        if (subscriber != null)
            await subscriber.DisposeAsync().ConfigureAwait(false);

        await Close().ConfigureAwait(false);
        disposed = true;
    }


    private InitializedConnection EnsureInitialized()
    {
        lock (initializedConnectionLock)
        {
            // For backwards compatibility. We have to wait for the Params property to be set,
            // and we can't introduce a new method for it either.
            if (initializedConnection is not null)
                return initializedConnection;

            var transport = transportFactory.Create(Params ?? new TapetiConnectionParams());
            transport.AttachObserver(new TransportObserver(this));

            initializedConnection = new InitializedConnection
            {
                Logger = logger,
                Transport = transport,
                DefaultConsumeChannel = new TapetiChannel(logger, transport, new TapetiChannelOptions
                {
                    ChannelType = ChannelType.ConsumeDefault,
                    PublisherConfirmationsEnabled = false
                }),

                DefaultPublishChannel = new TapetiChannel(logger, transport, new TapetiChannelOptions
                {
                    ChannelType = ChannelType.PublishDefault,
                    PublisherConfirmationsEnabled = true
                })
            };

            return initializedConnection;
        }
    }


    private class InitializedConnection
    {
        public required ILogger Logger { get; init; }
        public required ITapetiTransport Transport { get; init; }
        public required TapetiChannel DefaultConsumeChannel { get; init; }
        public required TapetiChannel DefaultPublishChannel { get; init; }
        public List<TapetiChannel> DedicatedChannels { get; } = [];


        public ITapetiChannel CreateDedicatedChannel()
        {
            var channel = new TapetiChannel(Logger, Transport, new TapetiChannelOptions
            {
                ChannelType = ChannelType.ConsumeDedicated,
                PublisherConfirmationsEnabled = false,
            });

            DedicatedChannels.Add(channel);

            return channel;
        }
    }

    private void TransportConnected(ConnectedEventArgs e)
    {
        var connectedEvent = Connected;
        if (connectedEvent != null)
            Task.Run(() => connectedEvent.Invoke(this, e));
    }


    private void TransportReconnected(ConnectedEventArgs e)
    {
        //subscriber?.Reconnect();

        var reconnectedEvent = Reconnected;
        if (reconnectedEvent != null)
            Task.Run(() => reconnectedEvent.Invoke(this, e));
    }


    private void TransportDisconnected(DisconnectedEventArgs e)
    {
        // TODO still required, or is it handled by the channel shutdown?
        subscriber?.Disconnect();

        var disconnectedEvent = Disconnected;
        if (disconnectedEvent != null)
            Task.Run(() => disconnectedEvent.Invoke(this, e));

        // TODO queue reconnect
    }


    private class TransportObserver : ITapetiTransportObserver
    {
        private readonly TapetiConnection owner;


        public TransportObserver(TapetiConnection owner)
        {
            this.owner = owner;
        }


        public void Connected(ConnectedEventArgs e)
        {
            owner.TransportConnected(e);
        }

        public void Reconnected(ConnectedEventArgs e)
        {
            owner.TransportReconnected(e);
        }

        public void Disconnected(DisconnectedEventArgs e)
        {
            owner.TransportDisconnected(e);
        }
    }
}
