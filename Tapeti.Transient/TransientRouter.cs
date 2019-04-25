using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using RabbitMQ.Client.Framing;
using Tapeti.Config;

namespace Tapeti.Transient
{
    public class TransientRouter
    {
        private readonly int defaultTimeoutMs;

        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<object>> map = new ConcurrentDictionary<Guid, TaskCompletionSource<object>>();


        public string TransientResponseQueueName { get; set; }

        public TransientRouter(TimeSpan defaultTimeout)
        {
            defaultTimeoutMs = (int)defaultTimeout.TotalMilliseconds;
        }

        public void GenericHandleResponse(object response, IMessageContext context)
        {
            if (context.Properties.CorrelationId == null)
                return;

            if (!Guid.TryParse(context.Properties.CorrelationId, out var continuationID))
                return;

            if (map.TryRemove(continuationID, out var tcs))
                tcs.SetResult(response);
        }

        public async Task<object> RequestResponse(IPublisher publisher, object request)
        {
            var correlation = Guid.NewGuid();
            var tcs = map.GetOrAdd(correlation, c => new TaskCompletionSource<object>());

            try
            {
                var properties = new BasicProperties
                {
                    CorrelationId = correlation.ToString(),
                    ReplyTo = TransientResponseQueueName,
                    Persistent = false
                };

                await ((IInternalPublisher)publisher).Publish(request, properties, false);
            }
            catch (Exception)
            {
                // Simple cleanup of the task and map dictionary.
                if (map.TryRemove(correlation, out tcs))
                    tcs.TrySetResult(null);

                throw;
            }

            using (new Timer(TimeoutResponse, tcs, defaultTimeoutMs, -1))
            {
                return await tcs.Task;
            }
        }

        private void TimeoutResponse(object tcs)
        {
            ((TaskCompletionSource<object>)tcs).SetException(new TimeoutException("Transient RequestResponse timed out at (ms) " + defaultTimeoutMs));
        }
    }
}