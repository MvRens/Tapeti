using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;
using Tapeti.Tasks;
using Tapeti.Transport;

namespace Tapeti.Connection;


internal class TapetiChannel : ITapetiChannel
{
    private readonly ILogger logger;
    private readonly ITapetiTransport transport;
    private readonly TapetiChannelOptions options;
    private ITapetiTransportChannel? transportChannel;

    private readonly List<ITapetiChannelObserver> observers = [];

    private readonly object taskQueueLock = new();
    private SerialTaskQueue? taskQueue;


    public TapetiChannel(ILogger logger, ITapetiTransport transport, TapetiChannelOptions options)
    {
        this.logger = logger;
        this.transport = transport;
        this.options = options;
    }


    public async Task Open()
    {
        await GetTaskQueue().Add(async () => await GetTransportChannel());
    }


    public async Task Close()
    {
        SerialTaskQueue? capturedTaskQueue;

        lock (taskQueueLock)
        {
            capturedTaskQueue = taskQueue;
            taskQueue = null;
        }

        if (capturedTaskQueue == null)
            return;

        await capturedTaskQueue.AddSync(() => { transportChannel = null; }).ConfigureAwait(false);

        capturedTaskQueue.Dispose();
    }


    public Task EnqueueOnce(Func<ITapetiTransportChannel, Task> operation)
    {
        return GetTaskQueue().Add(async () => await operation(await GetTransportChannel()));
    }

    public Task<T> EnqueueOnce<T>(Func<ITapetiTransportChannel, Task<T>> operation)
    {
        var result = new TaskCompletionSource<T>();

        GetTaskQueue().Add(async () =>
        {
            try
            {
                result.SetResult(await operation(await GetTransportChannel()));
            }
            catch (Exception e)
            {
                result.SetException(e);
            }
        });

        return result.Task;
    }


    public Task EnqueueRetry(Func<ITapetiTransportChannel, Task> operation, CancellationToken cancellationToken)
    {
        return GetTaskQueue().Add(() => RetryOperation(operation, cancellationToken));
    }

    public Task<T> EnqueueRetry<T>(Func<ITapetiTransportChannel, Task<T>> operation, CancellationToken cancellationToken)
    {
        var result = new TaskCompletionSource<T>();

        GetTaskQueue().Add(async () =>
        {
            try
            {
                await RetryOperation(async c =>
                {
                    result.SetResult(await operation(c));
                }, cancellationToken);
            }
            catch (Exception e)
            {
                result.SetException(e);
            }
        });

        return result.Task;
    }


    private async Task RetryOperation(Func<ITapetiTransportChannel, Task> operation, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var channel = await GetTransportChannel();
            try
            {
                await operation(channel);
                break;
            }
            // TODO is this enough error handling?
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
            IsRecreate = false
        });

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
        });

        var capturedTaskQueue = GetTaskQueue();
        await capturedTaskQueue.Add(async () =>
        {
            transportChannel = null;

            foreach (var observer in observers)
                await observer.OnShutdown(e);
        }).ConfigureAwait(false);


        if (e.IsClosing)
            return;

        // Try to reconnect the channel
        await capturedTaskQueue.Add(async () =>
        {
            transportChannel = await GetTransportChannel();

            foreach (var observer in observers)
                await observer.OnRecreated(transportChannel);
        }).ConfigureAwait(false);
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
