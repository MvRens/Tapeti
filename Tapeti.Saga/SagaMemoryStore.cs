using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tapeti.Saga
{
    public class SagaMemoryStore : ISagaStore
    {
        private ISagaStore decoratedStore;
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();


        // Not a constructor to allow standard injection to work when using only the MemoryStore
        public static SagaMemoryStore AsCacheFor(ISagaStore store)
        {
            return new SagaMemoryStore
            {
                decoratedStore = store
            };
        }


        public async Task<object> Read(string sagaId)
        {
            object value;

            // ReSharper disable once InvertIf
            if (!values.TryGetValue(sagaId, out value) && decoratedStore != null)
            {
                value = await decoratedStore.Read(sagaId);
                values.Add(sagaId, value);
            }

            return value;
        }

        public async Task Update(string sagaId, object state)
        {
            values[sagaId] = state;
            if (decoratedStore != null)
                await decoratedStore.Update(sagaId, state);
        }
    }
}
