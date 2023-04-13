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
using Tapeti.Helpers;

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
        public IYieldPoint YieldWithRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task<IYieldPoint>> responseHandler) where TRequest : class where TResponse : class
        {
            var responseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler);
            return new DelegateYieldPoint(context => SendRequest(context, message, responseHandlerInfo));
        }

        /// <inheritdoc />
        public IYieldPoint YieldWithRequest<TRequest, TResponse>(TRequest message, Func<TResponse, ValueTask<IYieldPoint>> responseHandler) where TRequest : class where TResponse : class
        {
            var responseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler);
            return new DelegateYieldPoint(context => SendRequest(context, message, responseHandlerInfo));
        }

        /// <inheritdoc />
        public IYieldPoint YieldWithRequestSync<TRequest, TResponse>(TRequest message, Func<TResponse, IYieldPoint> responseHandler) where TRequest : class where TResponse : class
        {
            var responseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler);
            return new DelegateYieldPoint(context => SendRequest(context, message, responseHandlerInfo));
        }

        /// <inheritdoc />
        public IFlowParallelRequestBuilder YieldWithParallelRequest()
        {
            return new ParallelRequestBuilder(config, this);
        }

        /// <inheritdoc />
        public IYieldPoint EndWithResponse<TResponse>(TResponse message) where TResponse : class
        {
            return new DelegateYieldPoint(context => SendResponse(context, message));
        }

        /// <inheritdoc />
        public IYieldPoint End()
        {
            return new DelegateYieldPoint(EndFlow);
        }


        internal async Task SendRequest(FlowContext context, object message, ResponseHandlerInfo responseHandlerInfo, 
            string? convergeMethodName = null, bool convergeMethodTaskSync = false, bool store = true)
        {
            if (!context.HasFlowStateAndLock)
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

            if (store)
                await context.Store(responseHandlerInfo.IsDurableQueue);

            await publisher.Publish(message, properties, true);
        }


        private async Task SendResponse(FlowContext context, object message)
        {
            var reply = context.HasFlowStateAndLock
                ? context.FlowState.Metadata.Reply
                : GetReply(context.HandlerContext);

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


        internal static async Task EndFlow(FlowContext context)
        {
            await context.Delete();

            if (context.HasFlowStateAndLock && context.FlowState.Metadata.Reply != null)
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

            return new ResponseHandlerInfo(
                MethodSerializer.Serialize(responseHandler.Method),
                binding.QueueName,
                binding.QueueType == QueueType.Durable
            );
        }


        private static ReplyMetadata? GetReply(IFlowHandlerContext context)
        {
            var requestAttribute = context.MessageContext?.Message?.GetType().GetCustomAttribute<RequestAttribute>();
            if (requestAttribute?.Response == null)
                return null;

            return new ReplyMetadata
            {
                CorrelationId = context.MessageContext!.Properties.CorrelationId,
                ReplyTo = context.MessageContext.Properties.ReplyTo,
                ResponseTypeName = requestAttribute.Response.FullName,
                Mandatory = context.MessageContext.Properties.Persistent.GetValueOrDefault(true)
            };
        }

        private static async Task CreateNewFlowState(FlowContext flowContext)
        {
            var flowStore = flowContext.HandlerContext.Config.DependencyResolver.Resolve<IFlowStore>();

            var flowID = Guid.NewGuid();
            var flowStateLock = await flowStore.LockFlowState(flowID);

            if (flowStateLock == null)
                throw new InvalidOperationException("Unable to lock a new flow");

            var flowState = new FlowState
            {
                Metadata = new FlowMetadata(GetReply(flowContext.HandlerContext))
            };

            flowContext.SetFlowState(flowState, flowStateLock);
        }

        
        /// <inheritdoc />
        public async ValueTask Execute(IFlowHandlerContext context, IYieldPoint yieldPoint)
        {
            if (yieldPoint is not DelegateYieldPoint executableYieldPoint)
                throw new YieldPointException($"Yield point is required in controller {context.Controller?.GetType().Name} for method {context.Method.Name}");

            FlowContext? flowContext = null;
            var disposeFlowContext = false;

            try
            {
                if (context.MessageContext == null || !context.MessageContext.TryGet<FlowMessageContextPayload>(out var flowPayload))
                {
                    flowContext = new FlowContext(context);

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
                    e.Data["Tapeti.Controller.Name"] = context.Controller?.GetType().FullName;
                    e.Data["Tapeti.Controller.Method"] = context.Method.Name;
                    throw;
                }

                flowContext.EnsureStoreOrDeleteIsCalled();
            }
            finally
            {
                if (disposeFlowContext)
                    flowContext?.Dispose();
            }
        }


        /// <inheritdoc />
        public IFlowParallelRequest? GetParallelRequest(IFlowHandlerContext context)
        {
            return context.MessageContext != null && context.MessageContext.TryGet<FlowMessageContextPayload>(out var flowPayload)
                ? new ParallelRequest(config, this, flowPayload.FlowContext)
                : null;
        }


        /// <inheritdoc />
        public ValueTask Converge(IFlowHandlerContext context)
        {
            return Execute(context, new DelegateYieldPoint(async flowContext =>
            {
                if (flowContext.ContinuationMetadata == null)
                    throw new InvalidOperationException("Missing ContinuationMetadata in FlowContext");

                if (flowContext.ContinuationMetadata.ConvergeMethodName == null)
                    throw new InvalidOperationException("Missing ConvergeMethodName in FlowContext ContinuationMetadata");

                await Converge(flowContext, flowContext.ContinuationMetadata.ConvergeMethodName, flowContext.ContinuationMetadata.ConvergeMethodSync);
            }));
        }


        internal async Task Converge(FlowContext flowContext, string convergeMethodName, bool convergeMethodSync)
        {
            IYieldPoint? yieldPoint;

            if (flowContext.HandlerContext == null)
                throw new InvalidOperationException($"Missing HandleContext in FlowContext for converge method {convergeMethodName}");

            if (flowContext.HandlerContext.MessageContext == null)
                throw new InvalidOperationException($"Missing MessageContext in FlowContext for converge method {convergeMethodName}");

            if (!flowContext.HandlerContext.MessageContext.TryGet<ControllerMessageContextPayload>(out var controllerPayload))
                throw new ArgumentException("Context does not contain a controller payload", nameof(flowContext));

            if (controllerPayload.Controller == null)
                throw new InvalidOperationException($"Controller is not available for converge method {convergeMethodName} (method is static?)");

            var method = controllerPayload.Controller.GetType().GetMethod(convergeMethodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                throw new ArgumentException($"Unknown converge method in controller {controllerPayload.Controller.GetType().Name}: {convergeMethodName}");

            if (convergeMethodSync)
                yieldPoint = (IYieldPoint?)method.Invoke(controllerPayload.Controller, new object[] { });
            else
            {
                var yieldPointTask = method.Invoke(controllerPayload.Controller, new object[] { });
                if (yieldPointTask == null)
                    throw new YieldPointException($"Yield point is required in controller {controllerPayload.Controller.GetType().Name} for converge method {convergeMethodName}");

                yieldPoint = await (Task<IYieldPoint>)yieldPointTask;
            }

            if (yieldPoint == null)
                throw new YieldPointException($"Yield point is required in controller {controllerPayload.Controller.GetType().Name} for converge method {convergeMethodName}");

            await Execute(flowContext.HandlerContext, yieldPoint);
        }



        private class ParallelRequestBuilder : IFlowParallelRequestBuilder
        {
            private class RequestInfo
            {
                public object Message { get; }
                public ResponseHandlerInfo ResponseHandlerInfo { get; }

                
                public RequestInfo(object message, ResponseHandlerInfo responseHandlerInfo)
                {
                    Message = message;
                    ResponseHandlerInfo = responseHandlerInfo;
                }
            }


            private readonly ITapetiConfig config;
            private readonly FlowProvider flowProvider;
            private readonly List<RequestInfo> requests = new();


            public ParallelRequestBuilder(ITapetiConfig config, FlowProvider flowProvider)
            {
                this.config = config;
                this.flowProvider = flowProvider;
            }


            public IFlowParallelRequestBuilder AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task> responseHandler) where TRequest : class where TResponse : class
            {
                return InternalAddRequest(message, responseHandler);
            }

            public IFlowParallelRequestBuilder AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, ValueTask> responseHandler) where TRequest : class where TResponse : class
            {
                return InternalAddRequest(message, responseHandler);
            }

            public IFlowParallelRequestBuilder AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, IFlowParallelRequest, Task> responseHandler) where TRequest : class where TResponse : class
            {
                return InternalAddRequest(message, responseHandler);
            }

            public IFlowParallelRequestBuilder AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, IFlowParallelRequest, ValueTask> responseHandler) where TRequest : class where TResponse : class
            {
                return InternalAddRequest(message, responseHandler);
            }

            public IFlowParallelRequestBuilder AddRequestSync<TRequest, TResponse>(TRequest message, Action<TResponse> responseHandler) where TRequest : class where TResponse : class
            {
                return InternalAddRequest(message, responseHandler);
            }

            public IFlowParallelRequestBuilder AddRequestSync<TRequest, TResponse>(TRequest message, Action<TResponse, IFlowParallelRequest> responseHandler) where TRequest : class where TResponse : class
            {
                return InternalAddRequest(message, responseHandler);
            }


            private IFlowParallelRequestBuilder InternalAddRequest(object message, Delegate responseHandler)
            {
                requests.Add(new RequestInfo(message, GetResponseHandlerInfo(config, message, responseHandler)));
                return this;
            }


            public IYieldPoint Yield(Func<Task<IYieldPoint>> continuation, FlowNoRequestsBehaviour noRequestsBehaviour = FlowNoRequestsBehaviour.Exception)
            {
                return BuildYieldPoint(continuation, false, noRequestsBehaviour);
            }

            public IYieldPoint YieldSync(Func<IYieldPoint> continuation, FlowNoRequestsBehaviour noRequestsBehaviour = FlowNoRequestsBehaviour.Exception)
            {
                return BuildYieldPoint(continuation, true, noRequestsBehaviour);
            }


            private IYieldPoint BuildYieldPoint(Delegate convergeMethod, bool convergeMethodSync, FlowNoRequestsBehaviour noRequestsBehaviour = FlowNoRequestsBehaviour.Exception)
            {
                if (requests.Count == 0)
                {
                    return noRequestsBehaviour switch
                    {
                        FlowNoRequestsBehaviour.Exception => throw new YieldPointException("At least one request must be added before yielding a parallel request"),
                        FlowNoRequestsBehaviour.Converge => new DelegateYieldPoint(context => flowProvider.Converge(context, convergeMethod.Method.Name, convergeMethodSync)),
                        FlowNoRequestsBehaviour.EndFlow => new DelegateYieldPoint(EndFlow),
                        _ => throw new ArgumentOutOfRangeException(nameof(noRequestsBehaviour), noRequestsBehaviour, null)
                    };
                }

                if (convergeMethod.Method == null)
                    throw new ArgumentNullException(nameof(convergeMethod));

                return new DelegateYieldPoint(async context =>
                {
                    if (convergeMethod.Method.DeclaringType != context.HandlerContext.Controller?.GetType())
                        throw new YieldPointException("Converge method must be in the same controller class");

                    foreach (var requestInfo in requests)
                    {
                        await flowProvider.SendRequest(
                            context,
                            requestInfo.Message,
                            requestInfo.ResponseHandlerInfo,
                            convergeMethod.Method.Name,
                            convergeMethodSync,
                            false);
                    }

                    await context.Store(requests.Any(i => i.ResponseHandlerInfo.IsDurableQueue));
                });
            }
        }


        private class ParallelRequest : IFlowParallelRequest
        {
            private readonly ITapetiConfig config;
            private readonly FlowProvider flowProvider;
            private readonly FlowContext flowContext;


            public ParallelRequest(ITapetiConfig config, FlowProvider flowProvider, FlowContext flowContext)
            {
                this.config = config;
                this.flowProvider = flowProvider;
                this.flowContext = flowContext;
            }


            public Task AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task> responseHandler) where TRequest : class where TResponse : class
            {
                return InternalAddRequest(message, responseHandler);
            }


            public Task AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, IFlowParallelRequest, Task> responseHandler) where TRequest : class where TResponse : class
            {
                return InternalAddRequest(message, responseHandler);
            }


            public Task AddRequestSync<TRequest, TResponse>(TRequest message, Action<TResponse> responseHandler) where TRequest : class where TResponse : class
            {
                return InternalAddRequest(message, responseHandler);
            }


            private Task InternalAddRequest(object message, Delegate responseHandler)
            {
                var responseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler);

                if (flowContext.ContinuationMetadata == null)
                    throw new InvalidOperationException("No ContinuationMetadata in FlowContext");

                return flowProvider.SendRequest(
                    flowContext, 
                    message, 
                    responseHandlerInfo, 
                    flowContext.ContinuationMetadata.ConvergeMethodName, 
                    flowContext.ContinuationMetadata.ConvergeMethodSync, 
                    false);
            }
        }


        internal class ResponseHandlerInfo
        {
            public string MethodName { get; }
            public string ReplyToQueue { get; }
            public bool IsDurableQueue { get; }


            public ResponseHandlerInfo(string methodName, string replyToQueue, bool isDurableQueue)
            {
                MethodName = methodName;
                ReplyToQueue = replyToQueue;
                IsDurableQueue = isDurableQueue;
            }
        }
    }
}
