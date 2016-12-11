using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Tapeti.Saga
{
    public class SagaProvider : ISagaProvider
    {
        protected static readonly ConcurrentDictionary<string, SemaphoreSlim> SagaLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly ISagaStore store;

        public SagaProvider(ISagaStore store)
        {
            this.store = store;
        }


        public async Task<ISaga<T>> Begin<T>(T initialState) where T : class
        {
            var saga = await Saga<T>.Create(() => Task.FromResult(initialState));
            await store.Update(saga.Id, saga.State);

            return saga;
        }

        public async Task<ISaga<T>> Continue<T>(string sagaId) where T : class
        {
            return await Saga<T>.Create(async () => await store.Read(sagaId) as T, sagaId);
        }

        public async Task<object> Continue(string sagaId)
        {
            return new Saga<object>
            {
                Id = sagaId,
                State = await store.Read(sagaId)
            };
        }


        protected class Saga<T> : ISaga<T> where T : class
        {
            private bool disposed;

            public string Id { get; set; }
            public T State { get; set; }


            public static async Task<Saga<T>> Create(Func<Task<T>> getState, string id = null)
            {
                var sagaId = id ?? Guid.NewGuid().ToString();
                await SagaLocks.GetOrAdd(sagaId, new SemaphoreSlim(1)).WaitAsync();

                var saga = new Saga<T>
                {
                    Id = sagaId,
                    State = await getState()
                };

                return saga;
            }


            public void Dispose()
            {
                if (disposed)
                    return;

                SemaphoreSlim semaphore;
                if (SagaLocks.TryGetValue(Id, out semaphore))
                    semaphore.Release();

                disposed = true;
            }


            public void ExpectResponse(string callId)
            {
                throw new NotImplementedException();
            }


            public void ResolveResponse(string callId)
            {
                throw new NotImplementedException();
            }
        }
    }
}
