using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tapeti.Tasks;
using Tapeti.Transport;

namespace Tapeti.Connection;


internal class TapetiChannel : ITapetiChannel
{
    private readonly ITapetiTransport transport;
    private readonly TapetiChannelOptions options;
    private ITapetiTransportChannel? transportChannel;

    private readonly List<ITapetiChannelObserver> observers = [];

    private readonly object taskQueueLock = new();
    private SerialTaskQueue? taskQueue;


    public TapetiChannel(ITapetiTransport transport, TapetiChannelOptions options)
    {
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


    public Task Enqueue(Func<ITapetiTransportChannel, Task> operation)
    {
        return GetTaskQueue().Add(async () => await operation(await GetTransportChannel()));
    }

    public Task<T> Enqueue<T>(Func<ITapetiTransportChannel, Task<T>> operation)
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


    public void AttachObserver(ITapetiChannelObserver observer)
    {
        observers.Add(observer);
    }


    private async Task<ITapetiTransportChannel> GetTransportChannel()
    {
        if (transportChannel is not null)
        {
            // TODO check if channel is still open
        }
        else
        {
            transportChannel = await transport.CreateChannel(options);
            transportChannel.AttachObserver(new TransportChannelObserver(this));
        }

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
        // TODO logging

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
