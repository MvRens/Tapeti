using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;
using Tapeti.Default;
using Tapeti.Tasks;
using Tapeti.Transport;

namespace Tapeti.Connection;


internal class TapetiChannel : ITapetiChannel
{
    public IMessageHandlerTracker MessageHandlerTracker { get; } = new MessageHandlerTracker();


    private readonly ILogger logger;
    private readonly ITapetiTransport transport;
    private readonly TapetiChannelOptions options;
    private ITapetiTransportChannel? transportChannel;
    private bool channelCreated;

    private readonly List<ITapetiChannelObserver> observers = [];

    private readonly object taskQueueLock = new();
    private SerialTaskQueue? taskQueue;
    private CancellationTokenSource reconnectCancellation = new();


    public TapetiChannel(ILogger logger, ITapetiTransport transport, TapetiChannelOptions options)
    {
        this.logger = logger;
        this.transport = transport;
        this.options = options;
    }


    internal async Task Open()
    {
        await GetTaskQueue().Add(async () => await GetTransportChannel());
    }


    internal async Task Close()
    {
        SerialTaskQueue? capturedTaskQueue;
        CancellationTokenSource capturedReconnectCancellation;

        lock (taskQueueLock)
        {
            capturedReconnectCancellation = reconnectCancellation;
            reconnectCancellation = new CancellationTokenSource();

            capturedTaskQueue = taskQueue;
            taskQueue = null;
        }

        await capturedReconnectCancellation.CancelAsync();

        if (capturedTaskQueue == null)
            return;

        await capturedTaskQueue.DisposeAsync();
    }


    public ValueTask EnqueueOnce(Func<ITapetiTransportChannel, ValueTask> operation)
    {
        return GetTaskQueue().Add(async () => await operation(await GetTransportChannel()));
    }

    public async ValueTask<T> EnqueueOnce<T>(Func<ITapetiTransportChannel, ValueTask<T>> operation)
    {
        T result = default!;

        await GetTaskQueue().Add(async () =>
        {
            result = await operation(await GetTransportChannel());
        });

        return result;
    }


    public ValueTask EnqueueRetry(Func<ITapetiTransportChannel, ValueTask> operation, CancellationToken cancellationToken)
    {
        return GetTaskQueue().Add(() => RetryOperation(operation, cancellationToken));
    }

    public async ValueTask<T> EnqueueRetry<T>(Func<ITapetiTransportChannel, ValueTask<T>> operation, CancellationToken cancellationToken)
    {
        T result = default!;

        await GetTaskQueue().Add(async () =>
        {
            await RetryOperation(async c =>
            {
                result = await operation(c);
            }, cancellationToken);

        });

        return result;
    }


    private async ValueTask RetryOperation(Func<ITapetiTransportChannel, ValueTask> operation, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var channel = await GetTransportChannel();
            try
            {
                await operation(channel);
                break;
            }
            catch (AlreadyClosedException)
            {
                // Retry
            }
        }
    }


    public void AttachObserver(ITapetiChannelObserver observer)
    {
        observers.Add(observer);
    }


    private async Task<ITapetiTransportChannel> GetTransportChannel()
    {
        if (transportChannel is not null && transportChannel.IsOpen)
            return transportChannel;

        transportChannel = await transport.CreateChannel(options);
        transportChannel.AttachObserver(new TransportChannelObserver(this));

        (logger as IChannelLogger)?.ChannelCreated(new ChannelCreatedContext
        {
            ChannelType = options.ChannelType,
            ConnectionReference = transportChannel.ConnectionReference,
            ChannelNumber = transportChannel.ChannelNumber,
            IsRecreate = channelCreated
        });

        channelCreated = true;
        return transportChannel;
    }


    private SerialTaskQueue GetTaskQueue()
    {
        lock (taskQueueLock)
        {
            return taskQueue ??= new SerialTaskQueue();
        }
    }


    private async ValueTask TransportChannelShutdown(ChannelShutdownEventArgs e)
    {
        if (transportChannel is null)
            return;

        (logger as IChannelLogger)?.ChannelShutdown(new ChannelShutdownContext
        {
            ChannelType = options.ChannelType,
            ConnectionReference = transportChannel.ConnectionReference,
            ChannelNumber = transportChannel.ChannelNumber,
            Initiator = e.Initiator,
            ReplyCode = e.ReplyCode.GetValueOrDefault(0),
            ReplyText = e.ReplyText ?? string.Empty,
            IsClosing = e.IsClosing
        });

        await GetTaskQueue().Add(async () =>
        {
            foreach (var observer in observers)
                await observer.OnShutdown(e);
        }).ConfigureAwait(false);

        await Close().ConfigureAwait(false);
        if (e.IsClosing)
            return;


        // Try to reconnect the channel
        // Note: since we've called Close, this is a new task queue and cancellation token source as well
        CancellationToken reconnectCancellationToken;

        lock (taskQueueLock)
        {
            reconnectCancellationToken = reconnectCancellation.Token;
        }


        _ = Task.Run(async () =>
        {
            try
            {
                await MessageHandlerTracker.WaitAll(Timeout.InfiniteTimeSpan, reconnectCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (reconnectCancellationToken.IsCancellationRequested)
                return;

            // TODO allow immediately the first time, then do an exponential back-off if the channel keeps getting shut down within the specified time
            //if (sameConnection && (DateTime.UtcNow - channelReference!.Value.CreatedDateTime).TotalMilliseconds <= MinimumChannelRecreateDelay)
            //  Thread.Sleep(ChannelRecreateDelay);


            await GetTaskQueue().Add(async () =>
            {
                transportChannel = await GetTransportChannel();

                foreach (var observer in observers)
                    await observer.OnRecreated(transportChannel);
            });
        }, CancellationToken.None);
    }


    private class TransportChannelObserver : ITapetiTransportChannelObserver
    {
        private readonly TapetiChannel owner;


        public TransportChannelObserver(TapetiChannel owner)
        {
            this.owner = owner;
        }


        public ValueTask OnShutdown(ChannelShutdownEventArgs e)
        {
            return owner.TransportChannelShutdown(e);
        }
    }
}
