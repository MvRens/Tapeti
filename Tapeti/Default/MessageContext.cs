using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Default
{
    internal class MessageContext : IMessageContext
    {
        private readonly Dictionary<Type, IMessageContextPayload> payloads = new();


        /// <inheritdoc />
        public ITapetiConfig Config { get; set; }

        /// <inheritdoc />
        public string Queue { get; set; }

        /// <inheritdoc />
        public string Exchange { get; set; }

        /// <inheritdoc />
        public string RoutingKey { get; set; }

        /// <inheritdoc />
        public byte[] RawBody { get; set; }

        /// <inheritdoc />
        public object Message { get; set; }

        /// <inheritdoc />
        public IMessageProperties Properties { get; set; }

        /// <inheritdoc />
        public IBinding Binding { get; set; }

        
        public void Store<T>(T payload) where T : IMessageContextPayload
        {
            payloads.Add(typeof(T), payload);
        }

        public void StoreOrUpdate<T>(Func<T> onAdd, Action<T> onUpdate) where T : IMessageContextPayload
        {
            if (payloads.TryGetValue(typeof(T), out var payload))
                onUpdate((T)payload);
            else
                payloads.Add(typeof(T), onAdd());
        }

        public T Get<T>() where T : IMessageContextPayload
        {
            return (T)payloads[typeof(T)];
        }

        public bool TryGet<T>(out T payload) where T : IMessageContextPayload
        {
            if (payloads.TryGetValue(typeof(T), out var payloadValue))
            {
                payload = (T)payloadValue;
                return true;
            }

            payload = default;
            return false;
        }


        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var payload in payloads.Values)
                (payload as IDisposable)?.Dispose();
        }


        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            foreach (var payload in payloads.Values)
            {
                if (payload is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
            }
        }



        /// <inheritdoc />
        public void Store(string key, object value)
        {
            StoreOrUpdate(
                () => new KeyValuePayload(key, value),
                payload => payload.Add(key, value));
        }


        /// <inheritdoc />
        public bool Get<T>(string key, out T value) where T : class
        {
            if (!TryGet<KeyValuePayload>(out var payload) ||
                !payload.TryGetValue(key, out var objectValue))
            {
                value = null;
                return false;
            }

            value = (T)objectValue;
            return true;
        }


        // ReSharper disable once InconsistentNaming
        public class KeyValuePayload : IMessageContextPayload, IDisposable, IAsyncDisposable
        {
            private readonly Dictionary<string, object> items = new();
            
            
            public KeyValuePayload(string key, object value)
            {
                Add(key, value);
            }
            
            
            public void Add(string key, object value)
            {
                items.Add(key, value);
            }


            public bool TryGetValue(string key, out object value)
            {
                return items.TryGetValue(key, out value);
            }
            
            
            public void Dispose()
            {
                foreach (var item in items.Values)
                    (item as IDisposable)?.Dispose();
            }

            
            public async ValueTask DisposeAsync()
            {
                foreach (var item in items.Values)
                {
                    if (item is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync();
                }
            }
        }
    }
}
