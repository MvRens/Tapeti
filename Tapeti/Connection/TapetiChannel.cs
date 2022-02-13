using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Tapeti.Tasks;

namespace Tapeti.Connection
{
    internal interface ITapetiChannelModelProvider
    {
        void WithChannel(Action<IModel> operation);
        void WithRetryableChannel(Action<IModel> operation);
    }
    
    
    /// <summary>
    /// Represents both a RabbitMQ Client Channel (IModel) as well as it's associated single-thread task queue.
    /// Access to the IModel is limited by design to enforce this relationship.
    /// </summary>
    internal class TapetiChannel
    {
        private readonly Func<IModel> modelFactory;
        private readonly object taskQueueLock = new object();
        private SingleThreadTaskQueue taskQueue;
        private readonly ModelProvider modelProvider;


        public TapetiChannel(Func<IModel> modelFactory)
        {
            this.modelFactory = modelFactory;
            modelProvider = new ModelProvider(this);
        }


        public async Task Reset()
        {
            SingleThreadTaskQueue capturedTaskQueue;
            
            lock (taskQueueLock)
            {
                capturedTaskQueue = taskQueue;
                taskQueue = null;
            }

            if (capturedTaskQueue == null)
                return;
            
            await capturedTaskQueue.Add(() => { });
            capturedTaskQueue.Dispose();
        }


        public Task Queue(Action<IModel> operation)
        {
            return GetTaskQueue().Add(() =>
            {
                modelProvider.WithChannel(operation);
            });
        }



        public Task QueueRetryable(Action<IModel> operation)
        {
            return GetTaskQueue().Add(() =>
            {
                modelProvider.WithRetryableChannel(operation);
            });
        }



        public Task QueueWithProvider(Func<ITapetiChannelModelProvider, Task> operation)
        {
            return GetTaskQueue().Add(async () =>
            {
                await operation(modelProvider);
            });
        }



        private SingleThreadTaskQueue GetTaskQueue()
        {
            lock (taskQueueLock)
            {
                return taskQueue ??= new SingleThreadTaskQueue();
            }
        }


        private class ModelProvider : ITapetiChannelModelProvider
        {
            private readonly TapetiChannel owner;

            
            public ModelProvider(TapetiChannel owner)
            {
                this.owner = owner;
            }

            
            public void WithChannel(Action<IModel> operation)
            {
                operation(owner.modelFactory());
            }
            

            public void WithRetryableChannel(Action<IModel> operation)
            {
                while (true)
                {
                    try
                    {
                        operation(owner.modelFactory());
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
