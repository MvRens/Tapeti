using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using RabbitMQ.Client.Framing;
using Tapeti.Config;
using Tapeti.Flow.Annotations;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Flow.Default
{
    public class FlowProvider : IFlowProvider, IFlowHandler
    {
        private readonly IConfig config;
        private readonly IInternalPublisher publisher;


        public FlowProvider(IConfig config, IPublisher publisher)
        {
            this.config = config;
            this.publisher = (IInternalPublisher)publisher;
        }


        public IYieldPoint YieldWithRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task<IYieldPoint>> responseHandler)
        {
            var responseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler);
            return new DelegateYieldPoint(true, context => SendRequest(context, message, responseHandlerInfo));
        }

        public IYieldPoint YieldWithRequestSync<TRequest, TResponse>(TRequest message, Func<TResponse, IYieldPoint> responseHandler)
        {
            var responseHandlerInfo = GetResponseHandlerInfo(config, message, responseHandler);
            return new DelegateYieldPoint(true, context => SendRequest(context, message, responseHandlerInfo));
        }

        public IFlowParallelRequestBuilder YieldWithParallelRequest()
        {
            return new ParallelRequestBuilder(config, SendRequest);
        }

        public IYieldPoint EndWithResponse<TResponse>(TResponse message)
        {
            return new DelegateYieldPoint(false, context => SendResponse(context, message));
        }

        public IYieldPoint End()
        {
            return new DelegateYieldPoint(false, EndFlow);
        }


        private async Task SendRequest(FlowContext context, object message, ResponseHandlerInfo responseHandlerInfo, 
            string convergeMethodName = null, bool convergeMethodTaskSync = false)
        {
            var continuationID = Guid.NewGuid();

            context.FlowState.Continuations.Add(continuationID,
                new ContinuationMetadata
                {
                    MethodName = responseHandlerInfo.MethodName,
                    ConvergeMethodName = convergeMethodName,
                    ConvergeMethodSync = convergeMethodTaskSync
                });

            var properties = new BasicProperties
            {
                CorrelationId = continuationID.ToString(),
                ReplyTo = responseHandlerInfo.ReplyToQueue
            };

            await context.EnsureStored();
            await publisher.Publish(message, properties);
        }


        private async Task SendResponse(FlowContext context, object message)
        {
            var reply = context.FlowState.Metadata.Reply;
            if (reply == null)
                throw new YieldPointException("No response is required");

            if (message.GetType().FullName != reply.ResponseTypeName)
                throw new YieldPointException($"Flow must end with a response message of type {reply.ResponseTypeName}, {message.GetType().FullName} was returned instead");

            var properties = new BasicProperties();

            // Only set the property if it's not null, otherwise a string reference exception can occur:
            // http://rabbitmq.1065348.n5.nabble.com/SocketException-when-invoking-model-BasicPublish-td36330.html
            if (reply.CorrelationId != null)
                properties.CorrelationId = reply.CorrelationId;

            // TODO disallow if replyto is not specified?
            if (context.FlowState.Metadata.Reply.ReplyTo != null)
                await publisher.PublishDirect(message, reply.ReplyTo, properties);
            else
                await publisher.Publish(message, properties);
        }


        private static Task EndFlow(FlowContext context)
        {
            if (context.FlowState.Metadata.Reply != null)
                throw new YieldPointException($"Flow must end with a response message of type {context.FlowState.Metadata.Reply.ResponseTypeName}");

            return Task.CompletedTask;
        }


        private static ResponseHandlerInfo GetResponseHandlerInfo(IConfig config, object request, Delegate responseHandler)
        {
            var binding = config.GetBinding(responseHandler);
            if (binding == null)
                throw new ArgumentException("responseHandler must be a registered message handler", nameof(responseHandler));

            var requestAttribute = request.GetType().GetCustomAttribute<RequestAttribute>();
            if (requestAttribute?.Response != null && requestAttribute.Response != binding.MessageClass)
                throw new ArgumentException($"responseHandler must accept message of type {binding.MessageClass}", nameof(responseHandler));

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
                ResponseTypeName = requestAttribute.Response.FullName
            };
        }


        public async Task Execute(IMessageContext context, IYieldPoint yieldPoint)
        {
            var executableYieldPoint = yieldPoint as IExecutableYieldPoint;
            var storeState = executableYieldPoint?.StoreState ?? false;

            FlowContext flowContext;
            object flowContextItem;

            if (!context.Items.TryGetValue(ContextItems.FlowContext, out flowContextItem))
            {
                flowContext = new FlowContext
                {
                    MessageContext = context
                };

                if (storeState)
                {
                    // Initiate the flow
                    var flowStore = context.DependencyResolver.Resolve<IFlowStore>();

                    var flowID = Guid.NewGuid();
                    flowContext.FlowStateLock = await flowStore.LockFlowState(flowID);

                    if (flowContext.FlowStateLock == null)
                        throw new InvalidOperationException("Unable to lock a new flow");

                    flowContext.FlowState = await flowContext.FlowStateLock.GetFlowState();
                    if (flowContext.FlowState == null)
                        throw new InvalidOperationException("Unable to get state for new flow");

                    flowContext.FlowState.Metadata.Reply = GetReply(context);
                }
            }
            else
                flowContext = (FlowContext) flowContextItem;


            try
            {
                if (executableYieldPoint != null)
                    await executableYieldPoint.Execute(flowContext);
            }
            catch (YieldPointException e)
            {
                var controllerName = flowContext.MessageContext.Controller.GetType().FullName;
                var methodName = flowContext.MessageContext.Binding.Method.Name;

                throw new YieldPointException($"{e.Message} in controller {controllerName}, method {methodName}", e);
            }

            if (storeState)
                await flowContext.EnsureStored();
            else
                await flowContext.FlowStateLock.DeleteFlowState();
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


            private readonly IConfig config;
            private readonly SendRequestFunc sendRequest;
            private readonly List<RequestInfo> requests = new List<RequestInfo>();


            public ParallelRequestBuilder(IConfig config, SendRequestFunc sendRequest)
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

                return new DelegateYieldPoint(true, context =>
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
