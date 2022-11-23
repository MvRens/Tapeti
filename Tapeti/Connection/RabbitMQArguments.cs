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


        public RabbitMQArguments(IReadOnlyDictionary<string, object> values) : base(values)
        {
        }


        public void AddUTF8(string key, string value)
        {
            Add(key, Encoding.UTF8.GetBytes(value));
        }
    }
}
