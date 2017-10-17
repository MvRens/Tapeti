using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using RabbitMQ.Client;
using Tapeti.Config;
using Tapeti.Default;
using Tapeti.Helpers;
using System.Threading.Tasks;

namespace Tapeti.Connection
{
    public class TapetiConsumer : DefaultBasicConsumer
    {
        private readonly TapetiWorker worker;
        private readonly string queueName;
        private readonly IDependencyResolver dependencyResolver;
        private readonly IReadOnlyList<IMessageMiddleware> messageMiddleware;
        private readonly IReadOnlyList<ICleanupMiddleware> cleanupMiddleware;
        private readonly List<IBinding> bindings;

        private readonly ILogger logger;
        private readonly IExceptionStrategy exceptionStrategy;


        public TapetiConsumer(TapetiWorker worker, string queueName, IDependencyResolver dependencyResolver, IEnumerable<IBinding> bindings, IReadOnlyList<IMessageMiddleware> messageMiddleware, IReadOnlyList<ICleanupMiddleware> cleanupMiddleware)
        {
            this.worker = worker;
            this.queueName = queueName;
            this.dependencyResolver = dependencyResolver;
            this.messageMiddleware = messageMiddleware;
            this.cleanupMiddleware = cleanupMiddleware;
            this.bindings = bindings.ToList();

            logger = dependencyResolver.Resolve<ILogger>();
            exceptionStrategy = dependencyResolver.Resolve<IExceptionStrategy>();
        }


        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            IBasicProperties properties, byte[] body)
        {
            Task.Run(async () =>
            {
                ExceptionDispatchInfo exception = null;
                MessageContext context = null;
                HandlingResult handlingResult = null;
                try
                {
                    try
                    {
                        context = new MessageContext
                        {
                            DependencyResolver = dependencyResolver,
                            Queue = queueName,
                            RoutingKey = routingKey,
                            Properties = properties
                        };

                        await DispatchMesage(context, body);

                        handlingResult = new HandlingResult
                        {
                            ConsumeResponse = ConsumeResponse.Ack,
                            MessageAction = MessageAction.None
                        };
                    }
                    catch (Exception eDispatch)
                    {
                        exception = ExceptionDispatchInfo.Capture(UnwrapException(eDispatch));
                        logger.HandlerException(eDispatch);
                        try
                        {
                            var exceptionStrategyContext = new ExceptionStrategyContext(context, exception.SourceException);

                            exceptionStrategy.HandleException(exceptionStrategyContext);

                            handlingResult = exceptionStrategyContext.HandlingResult.ToHandlingResult();
                        }
                        catch (Exception eStrategy)
                        {
                            logger.HandlerException(eStrategy);
                        }
                    }
                    try
                    {
                        if (handlingResult == null)
                        {
                            handlingResult = new HandlingResult
                            {
                                ConsumeResponse = ConsumeResponse.Nack,
                                MessageAction = MessageAction.None
                            };
                        }
                        await RunCleanup(context, handlingResult);
                    }
                    catch (Exception eCleanup)
                    {
                        logger.HandlerException(eCleanup);
                    }
                }
                finally
                {
                    try
                    {
                        if (handlingResult == null)
                        {
                            handlingResult = new HandlingResult
                            {
                                ConsumeResponse = ConsumeResponse.Nack,
                                MessageAction = MessageAction.None
                            };
                        }
                        await worker.Respond(deliveryTag, handlingResult.ConsumeResponse);
                    }
                    catch (Exception eRespond)
                    {
                        logger.HandlerException(eRespond);
                    }
                    try
                    {
                        if (context != null)
                        {
                            context.Dispose();
                        }
                    }
                    catch (Exception eDispose)
                    {
                        logger.HandlerException(eDispose);
                    }
                }
            });
        }

        private async Task RunCleanup(MessageContext context, HandlingResult handlingResult)
        {
            foreach(var handler in cleanupMiddleware)
            {
                try
                {
                    await handler.Handle(context, handlingResult);
                }
                catch (Exception eCleanup)
                {
                    logger.HandlerException(eCleanup);
                }
            }
        }

        private async Task DispatchMesage(MessageContext context, byte[] body)
        {
            var message = dependencyResolver.Resolve<IMessageSerializer>().Deserialize(body, context.Properties);
            if (message == null)
                throw new ArgumentException("Empty message");

            context.Message = message;

            var validMessageType = false;

            foreach (var binding in bindings)
            {
                if (binding.Accept(context, message))
                {
                    await InvokeUsingBinding(context, binding, message);

                    validMessageType = true;
                }
            }

            if (!validMessageType)
                throw new ArgumentException($"Unsupported message type: {message.GetType().FullName}");
        }

        private Task InvokeUsingBinding(MessageContext context, IBinding binding, object message)
        {
            context.Binding = binding;

            RecursiveCaller firstCaller = null;
            RecursiveCaller currentCaller = null;

            Action<Handler> addHandler = (Handler handle) =>
            {
                var caller = new RecursiveCaller(handle);
                if (currentCaller == null)
                    firstCaller = caller;
                else
                    currentCaller.next = caller;
                currentCaller = caller;
            };

            if (binding.MessageFilterMiddleware != null)
            {
                foreach (var m in binding.MessageFilterMiddleware)
                {
                    addHandler(m.Handle);
                }
            }

            addHandler(async (c, next) =>
            {
                c.Controller = dependencyResolver.Resolve(binding.Controller);
                await next();
            });

            foreach (var m in messageMiddleware)
            {
                addHandler(m.Handle);
            }

            if (binding.MessageMiddleware != null)
            {
                foreach (var m in binding.MessageMiddleware)
                {
                    addHandler(m.Handle);
                }
            }

            addHandler(async (c, next) =>
            {
                await binding.Invoke(c, message);
            });

            return firstCaller.Call(context);
        }

        private static Exception UnwrapException(Exception exception)
        {
            // In async/await style code this is handled similarly. For synchronous
            // code using Tasks we have to unwrap these ourselves to get the proper
            // exception directly instead of "Errors occured". We might lose
            // some stack traces in the process though.
            while (true)
            {
                var aggregateException = exception as AggregateException;
                if (aggregateException == null || aggregateException.InnerExceptions.Count != 1)
                    return exception;

                exception = aggregateException.InnerExceptions[0];
            }
        }
    }

    public delegate Task Handler(MessageContext context, Func<Task> next);

    public class RecursiveCaller
    {
        private Handler handle;
        private MessageContext currentContext;
        private MessageContext nextContext;
        public RecursiveCaller next;

        public RecursiveCaller(Handler handle)
        {
            this.handle = handle;
        }

        internal async Task Call(MessageContext context)
        {
            if (currentContext != null)
                throw new InvalidOperationException("Cannot simultaneously call 'next' in Middleware.");

            try
            {
                currentContext = context;

                context.UseNestedContext = next == null ? (Action<MessageContext>)null : UseNestedContext;

                await handle(context, callNext);
            }
            finally
            {
                currentContext = null;
            }
        }

        private async Task callNext()
        {
            if (next == null)
                return;
            if (nextContext != null)
            {
                await next.Call(nextContext);
            }else
            {
                try
                {
                    await next.Call(currentContext);
                }
                finally
                {
                    currentContext.UseNestedContext = UseNestedContext;
                }
            }
        }

        void UseNestedContext(MessageContext context)
        {
            if (nextContext != null)
                throw new InvalidOperationException("Previous nested context was not yet disposed.");

            context.OnContextDisposed = OnContextDisposed;
            nextContext = context;
        }

        void OnContextDisposed(MessageContext context)
        {
            context.OnContextDisposed = null;
            if (nextContext == context)
                nextContext = null;
        }
    }

}
