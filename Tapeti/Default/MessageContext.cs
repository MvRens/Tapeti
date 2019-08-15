using System;
using System.Collections.Generic;
using Tapeti.Config;

namespace Tapeti.Default
{
    internal class MessageContext : IMessageContext
    {
        private readonly Dictionary<string, object> items = new Dictionary<string, object>();


        /// <inheritdoc />
        public ITapetiConfig Config { get; set; }

        /// <inheritdoc />
        public string Queue { get; set; }

        /// <inheritdoc />
        public string Exchange { get; set; }

        /// <inheritdoc />
        public string RoutingKey { get; set; }

        /// <inheritdoc />
        public object Message { get; set; }

        /// <inheritdoc />
        public IMessageProperties Properties { get; set; }

        /// <inheritdoc />
        public IBinding Binding { get; set; }


        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var item in items.Values)
                (item as IDisposable)?.Dispose();
        }


        /// <inheritdoc />
        public void Store(string key, object value)
        {
            items.Add(key, value);
        }


        /// <inheritdoc />
        public bool Get<T>(string key, out T value) where T : class
        {
            if (!items.TryGetValue(key, out var objectValue))
            {
                value = default(T);
                return false;
            }

            value = (T)objectValue;
            return true;
        }
    }
}
