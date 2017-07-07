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
        private readonly List<IBinding> bindings;
        private readonly IExceptionStrategy exceptionStrategy;


        public TapetiConsumer(TapetiWorker worker, string queueName, IDependencyResolver dependencyResolver, IEnumerable<IBinding> bindings, IReadOnlyList<IMessageMiddleware> messageMiddleware)
        {
            this.worker = worker;
            this.queueName = queueName;
            this.dependencyResolver = dependencyResolver;
            this.messageMiddleware = messageMiddleware;
            this.bindings = bindings.ToList();

            exceptionStrategy = dependencyResolver.Resolve<IExceptionStrategy>();
        }


        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            IBasicProperties properties, byte[] body)
        {
            ExceptionDispatchInfo exception = null;
            try
            {
                var message = dependencyResolver.Resolve<IMessageSerializer>().Deserialize(body, properties);
                if (message == null)
                    throw new ArgumentException("Empty message");

                var validMessageType = false;

                using (var context = new MessageContext
                {
                    DependencyResolver = dependencyResolver,
                    Queue = queueName,
                    RoutingKey = routingKey,
                    Message = message,
                    Properties = properties
                })
                {
                    try
                    {
                        foreach (var binding in bindings)
                        {
                            if (binding.Accept(context, message))
                            {
                                InvokeUsingBinding(context, binding, message);

                                validMessageType = true;
                            }
                        }

                        if (!validMessageType)
                            throw new ArgumentException($"Unsupported message type: {message.GetType().FullName}");

                        worker.Respond(deliveryTag, ConsumeResponse.Ack);
                    }
                    catch (Exception e)
                    {
                        exception = ExceptionDispatchInfo.Capture(UnwrapException(e));
                        worker.Respond(deliveryTag, exceptionStrategy.HandleException(context, exception.SourceException));
                    }
                }
            }
            catch (Exception e)
            {
                exception = ExceptionDispatchInfo.Capture(UnwrapException(e));
                worker.Respond(deliveryTag, exceptionStrategy.HandleException(null, exception.SourceException));
            }

            exception?.Throw();
        }


        private void InvokeUsingBinding(MessageContext context, IBinding binding, object message)
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
                await binding.Invoke(context, message);
            });

            firstCaller.Call(context)
                .Wait();

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

    public class RecursiveCaller: ICallFrame
    {
        private Handler handle;
        private MessageContext context;
        private MessageContext nextContext;
        public RecursiveCaller next;

        public RecursiveCaller(Handler handle)
        {
            this.handle = handle;
        }

        internal async Task Call(MessageContext context)
        {
            if (this.context != null)
                throw new InvalidOperationException("Cannot simultaneously call 'next' in Middleware.");

            try
            {
                this.context = context;

                if (next != null)
                    context.SetCallFrame(this);

                await handle(context, callNext);
            }
            finally
            {
                context = null;
            }
        }

        private Task callNext()
        {
            if (next == null)
                return Task.CompletedTask;

            return next.Call(nextContext ?? context);
        }

        void ICallFrame.UseNestedContext(MessageContext context)
        {
            if (nextContext != null)
                throw new InvalidOperationException("Previous nested context was not yet disposed.");
            nextContext = context;
        }

        void ICallFrame.OnContextDisposed(MessageContext context)
        {
            if (nextContext == context)
                nextContext = null;
        }
    }

}
