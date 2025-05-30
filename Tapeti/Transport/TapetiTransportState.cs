using System.Collections.Generic;
using Tapeti.Default;

namespace Tapeti.Transport;

internal class TapetiTransportState
{
    public IMessageHandlerTracker MessageHandlerTracker { get; }


    private readonly HashSet<string> declaredExchanges = [];
    private readonly HashSet<string> deletedQueues = [];


    public TapetiTransportState()
    {
        MessageHandlerTracker = new MessageHandlerTracker();
    }


    public bool IsQueueDeleted(string queueName)
    {
        return deletedQueues.Contains(queueName);
    }

    public bool IsExchangeDeclared(string exchange)
    {
        return declaredExchanges.Contains(exchange);
    }

    public void SetExchangeDeclared(string exchange)
    {
        declaredExchanges.Add(exchange);
    }
}
