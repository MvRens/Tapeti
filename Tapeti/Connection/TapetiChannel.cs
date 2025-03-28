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


    internal delegate void AcquireModelProc(ref TapetiModelReference? modelReference);

    
    /// <summary>
    /// Represents both a RabbitMQ Client Channel (IModel) as well as it's associated single-thread task queue.
    /// Access to the IModel is limited by design to enforce this relationship.
    /// </summary>
    internal class TapetiChannel
    {
        private TapetiModelReference? modelReference;
        private readonly AcquireModelProc acquireModelProc;

        private readonly object taskQueueLock = new();
        private SerialTaskQueue? taskQueue;
        private readonly ModelProvider modelProvider;

        
        public TapetiChannel(AcquireModelProc acquireModelProc)
        {
            this.acquireModelProc = acquireModelProc;
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
            
            await capturedTaskQueue.Add(() =>
            {
                modelReference?.Model.Dispose();
                modelReference = null;
            }).ConfigureAwait(false);

            capturedTaskQueue.Dispose();
        }


        public void ClearModel()
        {
            modelReference = null;
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


        private IModel GetModel()
        {
            acquireModelProc(ref modelReference);
            return modelReference?.Model ?? throw new InvalidOperationException("RabbitMQ Model is unavailable");
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
                operation(owner.GetModel());
            }
            

            public void WithRetryableChannel(Action<IModel> operation)
            {
                while (true)
                {
                    try
                    {
                        operation(owner.GetModel());
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
