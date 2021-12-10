using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Annotations;
using Tapeti.Config;
using Tapeti.Default;
using Tapeti.Flow.Annotations;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Flow.Default
{
    /// <inheritdoc cref="IFlowProvider"/> />
    /// <summary>
    /// Default implementation for IFlowProvider.
    /// </summary>
    public class FlowProvider : IFlowProvider, IFlowHandler
    {
        private readonly ITapetiConfig config;
        private readonly IInternalPublisher publisher;


        /// <summary>
        /// </summary>
        public FlowProvider(ITapetiConfig config, IPublisher publisher)
        {
            this.config = config;
            this.publisher = (IInternalPublisher)publisher;
        }


        /// <inheritdoc />
        public IYieldPoint YieldWithRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task<IYieldPoint>> responseHandler)
        {
            var responseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler);
            return new DelegateYieldPoint(context => SendRequest(context, message, responseHandlerInfo));
        }

        /// <inheritdoc />
        public IYieldPoint YieldWithRequestSync<TRequest, TResponse>(TRequest message, Func<TResponse, IYieldPoint> responseHandler)
        {
            var responseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler);
            return new DelegateYieldPoint(context => SendRequest(context, message, responseHandlerInfo));
        }

        /// <inheritdoc />
        public IFlowParallelRequestBuilder YieldWithParallelRequest()
        {
            return new ParallelRequestBuilder(config, SendRequest);
        }

        /// <inheritdoc />
        public IYieldPoint EndWithResponse<TResponse>(TResponse message)
        {
            return new DelegateYieldPoint(context => SendResponse(context, message));
        }

        /// <inheritdoc />
        public IYieldPoint End()
        {
            return new DelegateYieldPoint(EndFlow);
        }


        private async Task SendRequest(FlowContext context, object message, ResponseHandlerInfo responseHandlerInfo, 
            string convergeMethodName = null, bool convergeMethodTaskSync = false)
        {
            if (context.FlowState == null)
            {
                await CreateNewFlowState(context);
                Debug.Assert(context.FlowState != null, "context.FlowState != null");
            }

            var continuationID = Guid.NewGuid();

            context.FlowState.Continuations.Add(continuationID,
                new ContinuationMetadata
                {
                    MethodName = responseHandlerInfo.MethodName,
                    ConvergeMethodName = convergeMethodName,
                    ConvergeMethodSync = convergeMethodTaskSync
                });

            var properties = new MessageProperties
            {
                CorrelationId = continuationID.ToString(),
                ReplyTo = responseHandlerInfo.ReplyToQueue
            };

            await context.Store(responseHandlerInfo.IsDurableQueue);

            await publisher.Publish(message, properties, true);
        }


        private async Task SendResponse(FlowContext context, object message)
        {
            var reply = context.FlowState == null
                ? GetReply(context.HandlerContext)
                : context.FlowState.Metadata.Reply;

            if (reply == null)
                throw new YieldPointException("No response is required");

            if (message.GetType().FullName != reply.ResponseTypeName)
                throw new YieldPointException($"Flow must end with a response message of type {reply.ResponseTypeName}, {message.GetType().FullName} was returned instead");

            var properties = new MessageProperties
            {
                CorrelationId = reply.CorrelationId
            };

            // TODO disallow if replyto is not specified?
            if (reply.ReplyTo != null)
                await publisher.PublishDirect(message, reply.ReplyTo, properties, reply.Mandatory);
            else
                await publisher.Publish(message, properties, reply.Mandatory);

            await context.Delete();
        }


        private static async Task EndFlow(FlowContext context)
        {
            await context.Delete();

            if (context.FlowState?.Metadata.Reply != null)
                throw new YieldPointException($"Flow must end with a response message of type {context.FlowState.Metadata.Reply.ResponseTypeName}");
        }


        private static ResponseHandlerInfo GetResponseHandlerInfo(ITapetiConfig config, object request, Delegate responseHandler)
        {
            var requestAttribute = request.GetType().GetCustomAttribute<RequestAttribute>();
            if (requestAttribute?.Response == null)
                throw new ArgumentException($"Request message {request.GetType().Name} must be marked with the Request attribute and a valid Response type", nameof(request));

            var binding = config.Bindings.ForMethod(responseHandler);
            if (binding == null)
                throw new ArgumentException("responseHandler must be a registered message handler", nameof(responseHandler));

            if (!binding.Accept(requestAttribute.Response))
                throw new ArgumentException($"responseHandler must accept message of type {requestAttribute.Response}", nameof(responseHandler));

            var continuationAttribute = binding.Method.GetCustomAttribute<ContinuationAttribute>();
            if (continuationAttribute == null)
                throw new ArgumentException("responseHandler must be marked with the Continuation attribute", nameof(responseHandler));

            if (binding.QueueName == null)
                throw new ArgumentException("responseHandler is not yet subscribed to a queue, TapetiConnection.Subscribe must be called before starting a flow", nameof(responseHandler));

            return new ResponseHandlerInfo
            {
                MethodName = MethodSerializer.Serialize(responseHandler.Method),
                ReplyToQueue = binding.QueueName,
                IsDurableQueue = binding.QueueType == QueueType.Durable
            };
        }


        private static ReplyMetadata GetReply(IFlowHandlerContext context)
        {
            var requestAttribute = context.MessageContext?.Message?.GetType().GetCustomAttribute<RequestAttribute>();
            if (requestAttribute?.Response == null)
                return null;

            return new ReplyMetadata
            {
                CorrelationId = context.MessageContext.Properties.CorrelationId,
                ReplyTo = context.MessageContext.Properties.ReplyTo,
                ResponseTypeName = requestAttribute.Response.FullName,
                Mandatory = context.MessageContext.Properties.Persistent.GetValueOrDefault(true)
            };
        }

        private static async Task CreateNewFlowState(FlowContext flowContext)
        {
            var flowStore = flowContext.HandlerContext.Config.DependencyResolver.Resolve<IFlowStore>();

            var flowID = Guid.NewGuid();
            flowContext.FlowStateLock = await flowStore.LockFlowState(flowID);

            if (flowContext.FlowStateLock == null)
                throw new InvalidOperationException("Unable to lock a new flow");

            flowContext.FlowState = new FlowState
            {
                Metadata = new FlowMetadata
                {
                    Reply = GetReply(flowContext.HandlerContext)
                }
            };
        }

        
        /// <inheritdoc />
        public async Task Execute(IFlowHandlerContext context, IYieldPoint yieldPoint)
        {
            if (!(yieldPoint is DelegateYieldPoint executableYieldPoint))
                throw new YieldPointException($"Yield point is required in controller {context.Controller.GetType().Name} for method {context.Method.Name}");

            FlowContext flowContext = null;
            var disposeFlowContext = false;

            try
            {
                var messageContext = context.MessageContext;
                if (messageContext == null || !messageContext.TryGet<FlowMessageContextPayload>(out var flowPayload))
                {
                    flowContext = new FlowContext
                    {
                        HandlerContext = context
                    };

                    // If we ended up here it is because of a Start. No point in storing the new FlowContext
                    // in the messageContext as the yield point is the last to execute.
                    disposeFlowContext = true;
                }
                else
                    flowContext = flowPayload.FlowContext;
                
                try
                {
                    await executableYieldPoint.Execute(flowContext);
                }
                catch (YieldPointException e)
                {
                    // Useful for debugging
                    e.Data["Tapeti.Controller.Name"] = context.Controller.GetType().FullName;
                    e.Data["Tapeti.Controller.Method"] = context.Method.Name;
                    throw;
                }

                flowContext.EnsureStoreOrDeleteIsCalled();
            }
            finally
            {
                if (disposeFlowContext)
                    flowContext.Dispose();
            }
        }


        /// <inheritdoc />
        public IFlowParallelRequest GetParallelRequest(IFlowHandlerContext context)
        {
            if (!context.MessageContext.TryGet<FlowMessageContextPayload>(out var flowPayload))
                return null;

            return new ParallelRequest(config, SendRequest, flowPayload.FlowContext);
        }


        private delegate Task SendRequestFunc(FlowContext context,
            object message,
            ResponseHandlerInfo responseHandlerInfo,
            string convergeMethodName,
            bool convergeMethodSync);


        private class ParallelRequestBuilder : IFlowParallelRequestBuilder
        {
            private class RequestInfo
            {
                public object Message { get; set; }
                public ResponseHandlerInfo ResponseHandlerInfo { get; set; }
            }


            private readonly ITapetiConfig config;
            private readonly SendRequestFunc sendRequest;
            private readonly List<RequestInfo> requests = new List<RequestInfo>();


            public ParallelRequestBuilder(ITapetiConfig config, SendRequestFunc sendRequest)
            {
                this.config = config;
                this.sendRequest = sendRequest;
            }


            public IFlowParallelRequestBuilder AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task> responseHandler)
            {
                return InternalAddRequest(message, responseHandler);
            }


            public IFlowParallelRequestBuilder AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, IFlowParallelRequest, Task> responseHandler)
            {
                return InternalAddRequest(message, responseHandler);
            }


            public IFlowParallelRequestBuilder AddRequestSync<TRequest, TResponse>(TRequest message, Action<TResponse> responseHandler)
            {
                return InternalAddRequest(message, responseHandler);
            }


            public IFlowParallelRequestBuilder InternalAddRequest(object message, Delegate responseHandler)
            {
                requests.Add(new RequestInfo
                {
                    Message = message,
                    ResponseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler)
                });

                return this;
            }


            public IYieldPoint Yield(Func<Task<IYieldPoint>> continuation)
            {
                return BuildYieldPoint(continuation, false);
            }


            public IYieldPoint YieldSync(Func<IYieldPoint> continuation)
            {
                return BuildYieldPoint(continuation, true);
            }


            private IYieldPoint BuildYieldPoint(Delegate convergeMethod, bool convergeMethodSync)
            {
                if (requests.Count == 0)
                    throw new YieldPointException("At least one request must be added before yielding a parallel request");

                if (convergeMethod?.Method == null)
                    throw new ArgumentNullException(nameof(convergeMethod));

                return new DelegateYieldPoint(context =>
                {
                    if (convergeMethod.Method.DeclaringType != context.HandlerContext.Controller.GetType())
                        throw new YieldPointException("Converge method must be in the same controller class");

                    return Task.WhenAll(requests.Select(requestInfo =>
                        sendRequest(context, requestInfo.Message,
                            requestInfo.ResponseHandlerInfo,
                            convergeMethod.Method.Name,
                            convergeMethodSync)));
                });
            }
        }


        private class ParallelRequest : IFlowParallelRequest
        {
            private readonly ITapetiConfig config;
            private readonly SendRequestFunc sendRequest;
            private readonly FlowContext flowContext;


            public ParallelRequest(ITapetiConfig config, SendRequestFunc sendRequest, FlowContext flowContext)
            {
                this.config = config;
                this.sendRequest = sendRequest;
                this.flowContext = flowContext;
            }


            public Task AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task> responseHandler)
            {
                return InternalAddRequest(message, responseHandler);
            }


            public Task AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, IFlowParallelRequest, Task> responseHandler)
            {
                return InternalAddRequest(message, responseHandler);
            }


            public Task AddRequestSync<TRequest, TResponse>(TRequest message, Action<TResponse> responseHandler)
            {
                return InternalAddRequest(message, responseHandler);
            }


            private Task InternalAddRequest(object message, Delegate responseHandler)
            {
                var responseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler);
                return sendRequest(flowContext, message, responseHandlerInfo, flowContext.ContinuationMetadata.ConvergeMethodName, flowContext.ContinuationMetadata.ConvergeMethodSync);
            }
        }


        internal class ResponseHandlerInfo
        {
            public string MethodName { get; set; }
            public string ReplyToQueue { get; set; }
            public bool IsDurableQueue { get; set; }
        }
    }
}
