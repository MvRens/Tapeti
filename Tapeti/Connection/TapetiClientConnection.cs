using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.Text;
using System.Threading;

namespace Tapeti.Connection
{
    internal readonly struct TapetiModelReference
    {
        public IModel Model { get; }
        public long ConnectionReference { get; }
        public DateTime CreatedDateTime { get; }


        public TapetiModelReference(IModel model, long connectionReference, DateTime createdDateTime)
        {
            Model = model;
            ConnectionReference = connectionReference;
            CreatedDateTime = createdDateTime;
        }
    }


    /// <summary>
    /// Implements a resilient connection to RabbitMQ.
    /// </summary>
    internal class TapetiClientConnection
    {
        /// <summary>
        /// Receives events when the connection state changes.
        /// </summary>
        public IConnectionEventListener? ConnectionEventListener { get; set; }

        public event Action? OnQueueReconnect;

        public bool IsClosing { get; private set; }


        private const int ReconnectDelay = 5000;
        private const int MinimumConnectedReconnectDelay = 1000;

        private const int ChannelRecreateDelay = 5000;
        private const int MinimumChannelRecreateDelay = 1000;


        private readonly ILogger logger;
        private readonly TapetiConnectionParams connectionParams;

        private readonly ConnectionFactory connectionFactory;


        // These fields must be locked using connectionLock
        private readonly object connectionLock = new();
        private long connectionReference;
        private RabbitMQ.Client.IConnection? connection;
        private bool isReconnect;
        private DateTime connectedDateTime;
        private IModel? connectionMonitorChannel;


        public TapetiClientConnection(ILogger logger, TapetiConnectionParams connectionParams)
        {
            this.logger = logger;
            this.connectionParams = connectionParams;

            connectionFactory = new ConnectionFactory
            {
                HostName = connectionParams.HostName,
                Port = connectionParams.Port,
                VirtualHost = connectionParams.VirtualHost,
                UserName = connectionParams.Username,
                Password = connectionParams.Password,
                AutomaticRecoveryEnabled = false,
                TopologyRecoveryEnabled = false,
                RequestedHeartbeat = TimeSpan.FromSeconds(30),
                DispatchConsumersAsync = true
            };

            if (connectionParams.ConsumerDispatchConcurrency > 0)
                connectionFactory.ConsumerDispatchConcurrency = connectionParams.ConsumerDispatchConcurrency;

            // ReSharper disable once InvertIf
            if (connectionParams.ClientProperties != null)
                foreach (var pair in connectionParams.ClientProperties)
                {
                    if (connectionFactory.ClientProperties.ContainsKey(pair.Key))
                        connectionFactory.ClientProperties[pair.Key] = Encoding.UTF8.GetBytes(pair.Value);
                    else
                        connectionFactory.ClientProperties.Add(pair.Key, Encoding.UTF8.GetBytes(pair.Value));
                }
        }


        public void Close()
        {
            RabbitMQ.Client.IConnection? capturedConnection;

            lock (connectionLock)
            {
                IsClosing = true;
                capturedConnection = connection;
                connection = null;
            }

            // ReSharper disable once InvertIf
            if (capturedConnection != null)
            {
                try
                {
                    capturedConnection.Close();
                }
                finally
                {
                    capturedConnection.Dispose();
                }
            }
        }


        public TapetiChannel CreateChannel(Action<IModel>? onInitModel)
        {
            var capturedChannel = new WeakReference<TapetiChannel?>(null);
            var channel = new TapetiChannel((ref TapetiModelReference? modelReference) =>
            {
                AcquireModel(ref modelReference, model =>
                {
                    model.ModelShutdown += (_, _) =>
                    {
                        if (capturedChannel.TryGetTarget(out var innerChannel))
                            innerChannel.ClearModel();
                    };

                    onInitModel?.Invoke(model);
                });
            });

            capturedChannel.SetTarget(channel);
            return channel;
        }



        private void AcquireModel(ref TapetiModelReference? modelReference, Action<IModel>? onInitModel)
        {
            var sameConnection = modelReference != null &&
                                 modelReference.Value.ConnectionReference == Interlocked.Read(ref connectionReference);

            if (sameConnection && modelReference!.Value.Model.IsOpen)
                return;

            long newConnectionReference;
            RabbitMQ.Client.IConnection capturedConnection;

            lock (connectionLock)
            {
                if (connection is not { IsOpen: true })
                {
                    try
                    {
                        connection?.Close();
                    }
                    catch (AlreadyClosedException)
                    {
                    }
                    finally
                    {
                        connection?.Dispose();
                    }

                    logger.Connect(new ConnectContext(connectionParams, isReconnect));
                    newConnectionReference = Interlocked.Increment(ref connectionReference);

                    connection = Connect();
                }
                else
                    newConnectionReference = Interlocked.Read(ref connectionReference);

                capturedConnection = connection;
            }

            if (sameConnection && (DateTime.UtcNow - modelReference!.Value.CreatedDateTime).TotalMilliseconds <= MinimumChannelRecreateDelay)
                Thread.Sleep(ChannelRecreateDelay);

            var newModel = capturedConnection.CreateModel();
            onInitModel?.Invoke(newModel);

            modelReference = new TapetiModelReference(newModel, newConnectionReference, DateTime.UtcNow);
        }


        private RabbitMQ.Client.IConnection Connect()
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
                    newConnection = connectionFactory.CreateConnection();
                    connectionMonitorChannel = newConnection.CreateModel();
                    if (connectionMonitorChannel == null)
                        throw new BrokerUnreachableException(null);

                    var capturedConnectionMonitorChannel = connectionMonitorChannel;

                    connectionMonitorChannel.ModelShutdown += (_, e) =>
                    {
                        bool capturedIsClosing;

                        lock (connectionLock)
                        {
                            if (connectionMonitorChannel == null || connectionMonitorChannel != capturedConnectionMonitorChannel)
                                return;

                            capturedConnectionMonitorChannel = null;
                            capturedIsClosing = IsClosing;
                        }

                        ConnectionEventListener?.Disconnected(new DisconnectedEventArgs(e.ReplyCode, e.ReplyText));
                        logger.Disconnect(new DisconnectContext(connectionParams, e.ReplyCode, e.ReplyText));

                        // Reconnect if the disconnect was unexpected
                        if (!capturedIsClosing)
                            // Note: I'm not too happy with this design, but letting the Client handle the reconnect is
                            // effectively how it was done before TapetiClientConnection was split off and since it
                            // manages the channels it is the best I could come up with for now.
                            OnQueueReconnect?.Invoke();
                    };

                    connectedDateTime = DateTime.UtcNow;

                    var connectedEventArgs = new ConnectedEventArgs(connectionParams, newConnection.LocalPort);

                    if (isReconnect)
                        ConnectionEventListener?.Reconnected(connectedEventArgs);
                    else
                        ConnectionEventListener?.Connected(connectedEventArgs);

                    logger.ConnectSuccess(new ConnectContext(connectionParams, isReconnect, newConnection.LocalPort));
                    isReconnect = true;

                    break;
                }
                catch (BrokerUnreachableException e)
                {
                    logger.ConnectFailed(new ConnectContext(connectionParams, isReconnect, exception: e));
                    Thread.Sleep(ReconnectDelay);
                }
            }

            return newConnection;
        }

        /// <summary>
        /// Returns the unique identifier of the current connection. Increments when the connection is lost.
        /// Can be used to detect if values related to the connection's lifetime, such as consumer tags,
        /// are still valid.
        /// </summary>
        public long GetConnectionReference()
        {
            return Interlocked.Read(ref connectionReference);
        }


        private class ConnectContext : IConnectSuccessContext, IConnectFailedContext
        {
            public TapetiConnectionParams ConnectionParams { get; }
            public bool IsReconnect { get; }
            public int LocalPort { get; }
            public Exception? Exception { get; }


            public ConnectContext(TapetiConnectionParams connectionParams, bool isReconnect, int localPort = 0, Exception? exception = null)
            {
                ConnectionParams = connectionParams;
                IsReconnect = isReconnect;
                LocalPort = localPort;
                Exception = exception;
            }
        }


        private class DisconnectContext : IDisconnectContext
        {
            public TapetiConnectionParams ConnectionParams { get; }
            public ushort ReplyCode { get; }
            public string ReplyText { get; }


            public DisconnectContext(TapetiConnectionParams connectionParams, ushort replyCode, string replyText)
            {
                ConnectionParams = connectionParams;
                ReplyCode = replyCode;
                ReplyText = replyText;
            }
        }
    }
}
