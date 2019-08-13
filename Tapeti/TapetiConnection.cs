using System;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Connection;

// ReSharper disable UnusedMember.Global

// TODO more separation from the actual worker / RabbitMQ Client for unit testing purposes

namespace Tapeti
{
    /// <inheritdoc />
    /// <summary>
    /// Creates a connection to RabbitMQ based on the provided Tapeti config.
    /// </summary>
    public class TapetiConnection : IConnection
    {
        private readonly ITapetiConfig config;

        /// <summary>
        /// Specifies the hostname and credentials to use when connecting to RabbitMQ.
        /// Defaults to guest on localhost.
        /// </summary>
        /// <remarks>
        /// This property must be set before first subscribing or publishing, otherwise it
        /// will use the default connection parameters.
        /// </remarks>
        public TapetiConnectionParams Params { get; set; }

        private readonly Lazy<TapetiClient> client;
        private TapetiSubscriber subscriber;

        /// <summary>
        /// Creates a new instance of a TapetiConnection and registers a default IPublisher
        /// in the IoC container as provided in the config.
        /// </summary>
        /// <param name="config"></param>
        public TapetiConnection(ITapetiConfig config)
        {
            this.config = config;
            (config.DependencyResolver as IDependencyContainer)?.RegisterDefault(GetPublisher);

            client = new Lazy<TapetiClient>(() => new TapetiClient(config, Params ?? new TapetiConnectionParams())
            {
                ConnectionEventListener = new ConnectionEventListener(this)
            });
        }

        /// <inheritdoc />
        public event EventHandler Connected;

        /// <inheritdoc />
        public event DisconnectedEventHandler Disconnected;

        /// <inheritdoc />
        public event EventHandler Reconnected;


        /// <inheritdoc />
        public async Task<ISubscriber> Subscribe(bool startConsuming = true)
        {
            if (subscriber == null)
            {
                subscriber = new TapetiSubscriber(() => client.Value, config);
                await subscriber.ApplyBindings();
            }

            if (startConsuming)
                await subscriber.Resume();

            return subscriber;
        }


        /// <inheritdoc />
        public ISubscriber SubscribeSync(bool startConsuming = true)
        {
            return Subscribe(startConsuming).Result;
        }


        /// <inheritdoc />
        public IPublisher GetPublisher()
        {
            return new TapetiPublisher(config, () => client.Value);
        }


        /// <inheritdoc />
        public async Task Close()
        {
            if (client.IsValueCreated)
                await client.Value.Close();
        }


        /// <inheritdoc />
        public void Dispose()
        {
            Close().Wait();
        }


        private class ConnectionEventListener: IConnectionEventListener
        {
            private readonly TapetiConnection owner;

            internal ConnectionEventListener(TapetiConnection owner)
            {
                this.owner = owner;
            }

            public void Connected()
            {
                owner.OnConnected(new EventArgs());
            }

            public void Disconnected(DisconnectedEventArgs e)
            {
                owner.OnDisconnected(e);
            }

            public void Reconnected()
            {
                owner.OnReconnected(new EventArgs());
            }
        }


        /// <summary>
        /// Called when a connection to RabbitMQ has been established.
        /// </summary>
        protected virtual void OnConnected(EventArgs e)
        {
            var connectedEvent = Connected;
            if (connectedEvent == null)
                return;

            Task.Run(() => connectedEvent.Invoke(this, e));
        }

        /// <summary>
        /// Called when the connection to RabbitMQ has been lost.
        /// </summary>
        protected virtual void OnReconnected(EventArgs e)
        {
            var reconnectedEvent = Reconnected;
            if (reconnectedEvent == null)
                return;

            Task.Run(() =>
            {
                subscriber?.ApplyBindings().ContinueWith((t) =>
                {
                    reconnectedEvent.Invoke(this, e);
                });
            });
        }

        /// <summary>
        /// Called when the connection to RabbitMQ has been recovered after an unexpected disconnect.
        /// </summary>
        protected virtual void OnDisconnected(DisconnectedEventArgs e)
        {
            var disconnectedEvent = Disconnected;
            if (disconnectedEvent == null)
                return;

            Task.Run(() => disconnectedEvent.Invoke(this, e));
        }
    }
}
