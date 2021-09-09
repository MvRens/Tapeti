using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Tapeti.Config;
using Tapeti.Default;

namespace Tapeti.Transient
{
    /// <summary>
    /// Manages active requests and responses. For internal use.
    /// </summary>
    internal class TransientRouter
    {
        private readonly int defaultTimeoutMs;
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<object>> map = new ConcurrentDictionary<Guid, TaskCompletionSource<object>>();

        /// <summary>
        /// The generated name of the dynamic queue to which responses should be sent.
        /// </summary>
        public string TransientResponseQueueName { get; set; }


        /// <summary>
        /// </summary>
        public TransientRouter(TimeSpan defaultTimeout)
        {
            defaultTimeoutMs = (int)defaultTimeout.TotalMilliseconds;
        }


        /// <summary>
        /// Processes incoming messages to complete the corresponding request task.
        /// </summary>
        /// <param name="context"></param>
        public void HandleMessage(IMessageContext context)
        {
            if (context.Properties.CorrelationId == null)
                return;

            if (!Guid.TryParse(context.Properties.CorrelationId, out var continuationID))
                return;

            if (map.TryRemove(continuationID, out var tcs))
                tcs.TrySetResult(context.Message);
        }


        /// <summary>
        /// Sends a request and waits for the response. Do not call directly, instead use ITransientPublisher.RequestResponse.
        /// </summary>
        /// <param name="publisher"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<object> RequestResponse(IPublisher publisher, object request)
        {
            var correlation = Guid.NewGuid();
            var tcs = map.GetOrAdd(correlation, c => new TaskCompletionSource<object>());

            try
            {
                var properties = new MessageProperties
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
            ((TaskCompletionSource<object>)tcs).TrySetException(new TimeoutException("Transient RequestResponse timed out at (ms) " + defaultTimeoutMs));
        }
    }
}