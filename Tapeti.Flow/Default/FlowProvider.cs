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
    public class FlowProvider : IFlowProvider, IFlowHandler
    {
        private readonly ITapetiConfig config;
        private readonly IInternalPublisher publisher;


        public FlowProvider(ITapetiConfig config, IPublisher publisher)
        {
            this.config = config;
            this.publisher = (IInternalPublisher)publisher;
        }


        public IYieldPoint YieldWithRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task<IYieldPoint>> responseHandler)
        {
            var responseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler);
            return new DelegateYieldPoint(context => SendRequest(context, message, responseHandlerInfo));
        }

        public IYieldPoint YieldWithRequestSync<TRequest, TResponse>(TRequest message, Func<TResponse, IYieldPoint> responseHandler)
        {
            var responseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler);
            return new DelegateYieldPoint(context => SendRequest(context, message, responseHandlerInfo));
        }

        public IFlowParallelRequestBuilder YieldWithParallelRequest()
        {
            return new ParallelRequestBuilder(config, SendRequest);
        }

        public IYieldPoint EndWithResponse<TResponse>(TResponse message)
        {
            return new DelegateYieldPoint(context => SendResponse(context, message));
        }

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

            await context.Store();

            await publisher.Publish(message, properties, true);
        }


        private async Task SendResponse(FlowContext context, object message)
        {
            var reply = context.FlowState == null
                ? GetReply(context.MessageContext)
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
            var binding = config.Bindings.ForMethod(responseHandler);
            if (binding == null)
                throw new ArgumentException("responseHandler must be a registered message handler", nameof(responseHandler));

            var requestAttribute = request.GetType().GetCustomAttribute<RequestAttribute>();
            if (requestAttribute?.Response != null && !binding.Accept(requestAttribute.Response))
                throw new ArgumentException($"responseHandler must accept message of type {requestAttribute.Response}", nameof(responseHandler));

            var continuationAttribute = binding.Method.GetCustomAttribute<ContinuationAttribute>();
            if (continuationAttribute == null)
                throw new ArgumentException("responseHandler must be marked with the Continuation attribute", nameof(responseHandler));

            if (binding.QueueName == null)
                throw new ArgumentException("responseHandler must bind to a valid queue", nameof(responseHandler));

            return new ResponseHandlerInfo
            {
                MethodName = MethodSerializer.Serialize(responseHandler.Method),
                ReplyToQueue = binding.QueueName
            };
        }


        private static ReplyMetadata GetReply(IMessageContext context)
        {
            var requestAttribute = context.Message?.GetType().GetCustomAttribute<RequestAttribute>();
            if (requestAttribute?.Response == null)
                return null;

            return new ReplyMetadata
            {
                CorrelationId = context.Properties.CorrelationId,
                ReplyTo = context.Properties.ReplyTo,
                ResponseTypeName = requestAttribute.Response.FullName,
                Mandatory = context.Properties.Persistent.GetValueOrDefault(true)
            };
        }

        private static async Task CreateNewFlowState(FlowContext flowContext)
        {
            var flowStore = flowContext.MessageContext.Config.DependencyResolver.Resolve<IFlowStore>();

            var flowID = Guid.NewGuid();
            flowContext.FlowStateLock = await flowStore.LockFlowState(flowID);

            if (flowContext.FlowStateLock == null)
                throw new InvalidOperationException("Unable to lock a new flow");

            flowContext.FlowState = new FlowState
            {
                Metadata = new FlowMetadata
                {
                    Reply = GetReply(flowContext.MessageContext)
                }
            };
        }

        public async Task Execute(IControllerMessageContext context, IYieldPoint yieldPoint)
        {
            if (!(yieldPoint is DelegateYieldPoint executableYieldPoint))
                throw new YieldPointException($"Yield point is required in controller {context.Controller.GetType().Name} for method {context.Binding.Method.Name}");

            if (!context.Get(ContextItems.FlowContext, out FlowContext flowContext))
            {
                flowContext = new FlowContext
                {
                    MessageContext = context
                };

                context.Store(ContextItems.FlowContext, flowContext);
            }

            try
            {
                await executableYieldPoint.Execute(flowContext);
            }
            catch (YieldPointException e)
            {
                // Useful for debugging
                e.Data["Tapeti.Controller.Name"] = context.Controller.GetType().FullName;
                e.Data["Tapeti.Controller.Method"] = context.Binding.Method.Name;
                throw;
            }

            flowContext.EnsureStoreOrDeleteIsCalled();
        }



        private class ParallelRequestBuilder : IFlowParallelRequestBuilder
        {
            public delegate Task SendRequestFunc(FlowContext context, 
                object message, 
                ResponseHandlerInfo responseHandlerInfo, 
                string convergeMethodName,
                bool convergeMethodSync);


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
                requests.Add(new RequestInfo
                {
                    Message = message,
                    ResponseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler)
                });

                return this;
            }


            public IFlowParallelRequestBuilder AddRequestSync<TRequest, TResponse>(TRequest message, Action<TResponse> responseHandler)
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
                if (convergeMethod?.Method == null)
                    throw new ArgumentNullException(nameof(convergeMethod));

                return new DelegateYieldPoint(context =>
                {
                    if (convergeMethod.Method.DeclaringType != context.MessageContext.Controller.GetType())
                        throw new YieldPointException("Converge method must be in the same controller class");

                    return Task.WhenAll(requests.Select(requestInfo =>
                        sendRequest(context, requestInfo.Message,
                            requestInfo.ResponseHandlerInfo,
                            convergeMethod.Method.Name,
                            convergeMethodSync)));
                });
            }
        }


        internal class ResponseHandlerInfo
        {
            public string MethodName { get; set; }
            public string ReplyToQueue { get; set; }
        }
    }
}
