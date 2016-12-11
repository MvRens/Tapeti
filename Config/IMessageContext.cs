using System.Collections.Generic;

namespace Tapeti.Config
{
    public interface IMessageContext
    {
        object Controller { get; }
        object Message { get; }
        IDictionary<string, object> Items { get; }
    }
}
