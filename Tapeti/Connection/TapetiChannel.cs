using System;
using System.Threading.Tasks;
using Tapeti.Tasks;
using Tapeti.Transport;

namespace Tapeti.Connection
{
    internal class TapetiChannel : ITapetiChannel
    {
        public TapetiChannelRecreatedEvent? OnRecreated { get; set; }


        private readonly ITapetiTransport transport;
        private readonly TapetiChannelOptions options;
        private ITapetiTransportChannel? transportChannel;

        private readonly object taskQueueLock = new();
        private SerialTaskQueue? taskQueue;


        public TapetiChannel(ITapetiTransport transport, TapetiChannelOptions options)
        {
            this.transport = transport;
            this.options = options;
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

            await capturedTaskQueue.AddSync(() =>
            {
                transportChannel = null;
            }).ConfigureAwait(false);

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


        private async Task<ITapetiTransportChannel> GetTransportChannel()
        {
            if (transportChannel is not null)
            {
                // TODO check if channel is still open
            }
            else
                transportChannel = await transport.CreateChannel(options);

            return transportChannel;
        }


        private SerialTaskQueue GetTaskQueue()
        {
            lock (taskQueueLock)
            {
                return taskQueue ??= new SerialTaskQueue();
            }
        }
    }
}
