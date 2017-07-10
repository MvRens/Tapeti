using System;
using System.Collections;
using System.Collections.Generic;
using RabbitMQ.Client;
using Tapeti.Config;
using System.Linq;

namespace Tapeti.Default
{
    public class MessageContext : IMessageContext
    {
        public IDependencyResolver DependencyResolver { get; set; }

        public object Controller { get; set; }
        public IBinding Binding { get; set; }

        public string Queue { get; set; }
        public string RoutingKey { get; set; }
        public object Message { get; set; }
        public IBasicProperties Properties { get; set; }

        public IDictionary<string, object> Items { get; }

        private readonly MessageContext outerContext;
        internal Action<MessageContext> UseNestedContext;
        internal Action<MessageContext> OnContextDisposed;

        public MessageContext()
        {
            Items = new Dictionary<string, object>();
        }

        private MessageContext(MessageContext outerContext)
        {
            DependencyResolver = outerContext.DependencyResolver;

            Controller = outerContext.Controller;
            Binding = outerContext.Binding;

            Queue = outerContext.Queue;
            RoutingKey = outerContext.RoutingKey;
            Message = outerContext.Message;
            Properties = outerContext.Properties;

            Items = new DeferingDictionary(outerContext.Items);

            this.outerContext = outerContext;
        }

        public void Dispose()
        {
            var items = (Items as DeferingDictionary)?.MyState ?? Items;

            foreach (var value in items.Values)
                (value as IDisposable)?.Dispose();

            OnContextDisposed?.Invoke(this);
        }

        public IMessageContext SetupNestedContext()
        {
            if (UseNestedContext == null)
                throw new NotSupportedException("This context does not support creating nested contexts");

            var nested = new MessageContext(this);

            UseNestedContext(nested);

            return nested;
        }

        private class DeferingDictionary : IDictionary<string, object>
        {
            private IDictionary<string, object> myState;
            private IDictionary<string, object> deferee;

            public DeferingDictionary(IDictionary<string, object> deferee)
            {
                myState = new Dictionary<string, object>();
                this.deferee = deferee;
            }

            public IDictionary<string, object> MyState => myState;

            object IDictionary<string, object>.this[string key]
            {
                get
                {
                    return myState.ContainsKey(key) ? myState[key] : deferee[key];
                }

                set
                {
                    if (deferee.ContainsKey(key))
                        throw new InvalidOperationException("Cannot hide an item set in an outer context.");

                    myState[key] = value;
                }
            }

            int ICollection<KeyValuePair<string, object>>.Count
            {
                get
                {
                    return myState.Count + deferee.Count;
                }
            }

            bool ICollection<KeyValuePair<string, object>>.IsReadOnly
            {
                get
                {
                    return false;
                }
            }

            ICollection<string> IDictionary<string, object>.Keys
            {
                get
                {
                    return myState.Keys.Concat(deferee.Keys).ToList().AsReadOnly();
                }
            }

            ICollection<object> IDictionary<string, object>.Values
            {
                get
                {
                    return myState.Values.Concat(deferee.Values).ToList().AsReadOnly();
                }
            }

            void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
            {
                if (deferee.ContainsKey(item.Key))
                    throw new InvalidOperationException("Cannot hide an item set in an outer context.");

                myState.Add(item);
            }

            void IDictionary<string, object>.Add(string key, object value)
            {
                if (deferee.ContainsKey(key))
                    throw new InvalidOperationException("Cannot hide an item set in an outer context.");

                myState.Add(key, value);
            }

            void ICollection<KeyValuePair<string, object>>.Clear()
            {
                throw new InvalidOperationException("Cannot influence the items in an outer context.");
            }

            bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
            {
                return myState.Contains(item) || deferee.Contains(item);
            }

            bool IDictionary<string, object>.ContainsKey(string key)
            {
                return myState.ContainsKey(key) || deferee.ContainsKey(key);
            }

            void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
            {
                foreach(var item in myState.Concat(deferee))
                {
                    array[arrayIndex++] = item;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return (IEnumerator)myState.Concat(deferee);
            }

            IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
            {
                return (IEnumerator < KeyValuePair < string, object>> )myState.Concat(deferee);
            }

            bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
            {
                if (deferee.ContainsKey(item.Key))
                    throw new InvalidOperationException("Cannot remove an item set in an outer context.");

                return myState.Remove(item);
            }

            bool IDictionary<string, object>.Remove(string key)
            {
                if (deferee.ContainsKey(key))
                    throw new InvalidOperationException("Cannot remove an item set in an outer context.");

                return myState.Remove(key);
            }

            bool IDictionary<string, object>.TryGetValue(string key, out object value)
            {
                return myState.TryGetValue(key, out value) 
                    || deferee.TryGetValue(key, out value);
            }
        }
    }
}
