using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Tapeti.Tasks;

namespace Tapeti.Connection
{
    internal interface ITapetiChannelModelProvider
    {
        Task WithChannel(Func<IChannel, Task> operation);
        Task WithRetryableChannel(Func<IChannel, Task> operation);
    }


    internal delegate Task<TapetiChannelReference> AcquireChannelProc(TapetiChannelReference? channelReference);

    
    /// <summary>
    /// Represents both a RabbitMQ Client Channel (IModel) as well as it's associated single-thread task queue.
    /// Access to the IModel is limited by design to enforce this relationship.
    /// </summary>
    internal class TapetiChannel
    {
        private TapetiChannelReference? channelReference;
        private readonly AcquireChannelProc acquireChannelProc;

        private readonly object taskQueueLock = new();
        private SerialTaskQueue? taskQueue;
        private readonly ModelProvider modelProvider;

        
        public TapetiChannel(AcquireChannelProc acquireChannelProc)
        {
            this.acquireChannelProc = acquireChannelProc;
            modelProvider = new ModelProvider(this);
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
                channelReference?.Channel.Dispose();
                channelReference = null;
            }).ConfigureAwait(false);

            capturedTaskQueue.Dispose();
        }


        public void ClearModel()
        {
            channelReference = null;
        }


        public Task Queue(Func<IChannel, Task> operation)
        {
            return GetTaskQueue().Add(() => modelProvider.WithChannel(operation));
        }



        public Task QueueRetryable(Func<IChannel, Task> operation)
        {
            return GetTaskQueue().Add(() => modelProvider.WithRetryableChannel(operation));
        }



        public Task QueueWithProvider(Func<ITapetiChannelModelProvider, Task> operation)
        {
            return GetTaskQueue().Add(async () =>
            {
                await operation(modelProvider).ConfigureAwait(false);
            });
        }



        private SerialTaskQueue GetTaskQueue()
        {
            lock (taskQueueLock)
            {
                return taskQueue ??= new SerialTaskQueue();
            }
        }


        private async Task<IChannel> GetChannel()
        {
            channelReference = await acquireChannelProc(channelReference);
            return channelReference?.Channel ?? throw new InvalidOperationException("RabbitMQ Model is unavailable");
        }


        private class ModelProvider : ITapetiChannelModelProvider
        {
            private readonly TapetiChannel owner;

            
            public ModelProvider(TapetiChannel owner)
            {
                this.owner = owner;
            }

            
            public async Task WithChannel(Func<IChannel, Task> operation)
            {
                await operation(await owner.GetChannel());
            }
            

            public async Task WithRetryableChannel(Func<IChannel, Task> operation)
            {
                while (true)
                {
                    try
                    {
                        await operation(await owner.GetChannel());
                        break;
                    }
                    catch (AlreadyClosedException)
                    {
                    }
                }
            }
        }
    }
}
