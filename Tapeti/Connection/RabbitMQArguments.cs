using System.Collections.Generic;
using System.Text;

namespace Tapeti.Connection
{
    /// <inheritdoc />
    public interface IRabbitMQArguments : IReadOnlyDictionary<string, object>
    {
    }


    internal class RabbitMQArguments : Dictionary<string, object>, IRabbitMQArguments
    {
        public RabbitMQArguments()
        {
        }

        #if NETSTANDARD2_1_OR_GREATER
        public RabbitMQArguments(IReadOnlyDictionary<string, object> values) : base(values)
        {
        }
        #else
        public RabbitMQArguments(IReadOnlyDictionary<string, object> values)
        {
            foreach (var pair in values)
                Add(pair.Key, pair.Value);
        }
        #endif


        public void AddUTF8(string key, string value)
        {
            Add(key, Encoding.UTF8.GetBytes(value));
        }
    }
}
