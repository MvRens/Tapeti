using System;
using System.Collections.Generic;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <inheritdoc cref="IControllerMessageContext" />
    public class ControllerMessageContext : MessageContext, IControllerMessageContext
    {
        private readonly Dictionary<string, object> items = new Dictionary<string, object>();


        /// <inheritdoc />
        public object Controller { get; set; }

        /// <inheritdoc />
        public new IControllerMethodBinding Binding { get; set; }


        /// <inheritdoc />
        public override void Dispose()
        {
            foreach (var item in items.Values)
                (item as IDisposable)?.Dispose();                

            base.Dispose();
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
