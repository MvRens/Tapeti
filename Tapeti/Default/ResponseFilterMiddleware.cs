using System;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Helpers;

namespace Tapeti.Default
{
    /// <inheritdoc cref="IControllerMessageMiddleware"/> />
    /// <summary>
    /// Handles methods marked with the ResponseHandler attribute.
    /// </summary>
    internal class ResponseFilterMiddleware : IControllerFilterMiddleware//, IControllerMessageMiddleware
    {
        internal const string CorrelationIdRequestPrefix = "request|";


        public async ValueTask Filter(IMessageContext context, Func<ValueTask> next)
        {
            if (!context.TryGet<ControllerMessageContextPayload>(out var controllerPayload))
                return;

            // If no CorrelationId is present, this could be a request-response in flight from a previous version of
            // Tapeti so we should not filter the response handler.
            if (!string.IsNullOrEmpty(context.Properties.CorrelationId))
            {
                if (!context.Properties.CorrelationId.StartsWith(CorrelationIdRequestPrefix))
                    return;

                var methodName = context.Properties.CorrelationId[CorrelationIdRequestPrefix.Length..];
                if (methodName != MethodSerializer.Serialize(controllerPayload.Binding.Method))
                    return;
            }

            await next().ConfigureAwait(false);
        }
    }
}
