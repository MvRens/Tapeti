using System.Collections.Generic;

namespace Tapeti.Transport;

internal class TapetiTransportState
{
    private readonly HashSet<string> declaredExchanges = [];
    private readonly HashSet<string> deletedQueues = [];


    public bool IsQueueDeleted(string queueName)
    {
        return deletedQueues.Contains(queueName);
    }

    public void SetQueueDeleted(string queueName)
    {
        deletedQueues.Add(queueName);
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
