using System;
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
        private readonly IAdvancedPublisher publisher;


        public FlowProvider(IConfig config, IPublisher publisher)
        {
            this.config = config;
            this.publisher = (IAdvancedPublisher)publisher;
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
            throw new NotImplementedException();
            //return new ParallelRequestBuilder();
        }

        public IYieldPoint EndWithResponse<TResponse>(TResponse message)
        {
            return new DelegateYieldPoint(false, context => SendResponse(context, message));
        }

        public IYieldPoint End()
        {
            return new DelegateYieldPoint(false, EndFlow);
        }


        private async Task SendRequest(FlowContext context, object message, ResponseHandlerInfo responseHandlerInfo)
        {
            var continuationID = Guid.NewGuid();

            context.FlowState.Continuations.Add(continuationID,
                Newtonsoft.Json.JsonConvert.SerializeObject(new ContinuationMetadata
                {
                    MethodName = responseHandlerInfo.MethodName,
                    ConvergeMethodName = null
                }));

            var properties = new BasicProperties
            {
                CorrelationId = continuationID.ToString(),
                ReplyTo = responseHandlerInfo.ReplyToQueue
            };

            await publisher.Publish(message, properties);
        }


        private async Task SendResponse(FlowContext context, object message)
        {
            if (context.Reply == null)
                throw new InvalidOperationException("No response is required");

            if (message.GetType().FullName != context.Reply.ResponseTypeName)
                throw new InvalidOperationException($"Flow must end with a response message of type {context.Reply.ResponseTypeName}, {message.GetType().FullName} was returned instead");

            var properties = new BasicProperties
            {
                CorrelationId = context.Reply.CorrelationId
            };

            await publisher.PublishDirect(message, context.Reply.ReplyTo, properties);
        }


        private static Task EndFlow(FlowContext context)
        {
            if (context.Reply != null)
                throw new InvalidOperationException($"Flow must end with a response message of type {context.Reply.ResponseTypeName}");

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

            return new ResponseHandlerInfo
            {
                MethodName = MethodSerializer.Serialize(responseHandler.Method),
                ReplyToQueue = binding.QueueName
            };
        }


        public async Task Execute(IMessageContext context, IYieldPoint yieldPoint)
        {
            var flowContext = (FlowContext)context.Items[ContextItems.FlowContext];
            if (flowContext == null)
                return;

            var delegateYieldPoint = (DelegateYieldPoint)yieldPoint;
            await delegateYieldPoint.Execute(flowContext);

            if (delegateYieldPoint.StoreState)
            {
                flowContext.FlowState.Data = Newtonsoft.Json.JsonConvert.SerializeObject(context.Controller);
                await flowContext.FlowStateLock.StoreFlowState(flowContext.FlowState);
            }
            else
            {
                await flowContext.FlowStateLock.DeleteFlowState();
            }
        }


        /*
        private class ParallelRequestBuilder : IFlowParallelRequestBuilder
        {
            internal class RequestInfo
            {
                public object Message { get; set; }
                public ResponseHandlerInfo ResponseHandlerInfo { get; set; }
            }


            private readonly IConfig config;
            private readonly IFlowStore flowStore;
            private readonly Func<FlowContext, object, ResponseHandlerInfo, Task> sendRequest;
            private readonly List<RequestInfo> requests = new List<RequestInfo>();


            public ParallelRequestBuilder(IConfig config, IFlowStore flowStore, Func<FlowContext, object, ResponseHandlerInfo, Task> sendRequest)
            {
                this.config = config;
                this.flowStore = flowStore;
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
                return new YieldPoint(flowStore, true, context => Task.WhenAll(requests.Select(requestInfo => sendRequest(context, requestInfo.Message, requestInfo.ResponseHandlerInfo))));
            }


            public IYieldPoint Yield(Func<IYieldPoint> continuation)
            {
                return new YieldPoint(flowStore, true, context => Task.WhenAll(requests.Select(requestInfo => sendRequest(context, requestInfo.Message, requestInfo.ResponseHandlerInfo))));
            }
        }*/


        internal class ResponseHandlerInfo
        {
            public string MethodName { get; set; }
            public string ReplyToQueue { get; set; }
        }



        /*
         * Handle response (correlationId known)
            internal async Task HandleMessage(object message, string correlationID)
            {
                var continuationID = Guid.Parse(correlationID);
                var flowStateID = await owner.flowStore.FindFlowStateID(continuationID);

                if (!flowStateID.HasValue)
                    return;

                using (flowStateLock = await owner.flowStore.LockFlowState(flowStateID.Value))
                {
                    flowState = await flowStateLock.GetFlowState();

                    continuation = flowState.Continuations[continuationID];
                    if (continuation != null)
                        await HandleContinuation(message);
                }
            }

            private async Task HandleContinuation(object message)
            {
                var flowMetaData = Newtonsoft.Json.JsonConvert.DeserializeObject<FlowMetaData>(flowState.MetaData);
                var continuationMetaData =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<ContinuationMetaData>(continuation.MetaData);

                reply = flowMetaData.Reply;
                controllerType = owner.GetControllerType(flowMetaData.ControllerTypeName);
                method = controllerType.GetMethod(continuationMetaData.MethodName);

                controller = owner.container.GetInstance(controllerType);

                Newtonsoft.Json.JsonConvert.PopulateObject(flowState.Data, controller);

                var yieldPoint = (AbstractYieldPoint) await owner.CallFlowController(controller, method, message);

                await yieldPoint.Execute(this);

                if (yieldPoint.Store)
                {
                    flowState.Data = Newtonsoft.Json.JsonConvert.SerializeObject(controller);
                    flowState.Continuations.Remove(continuation);

                    await flowStateLock.StoreFlowState(flowState);
                }
                else
                {
                    await flowStateLock.DeleteFlowState();
                }
            }

        }
        */
    }
}
