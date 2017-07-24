using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Annotations;
using Tapeti.Config;
using Tapeti.Default;
using Tapeti.Helpers;


namespace Tapeti
{
    public class TopologyConfigurationException : Exception
    {
        public TopologyConfigurationException(string message) : base(message) { }
    }

    public delegate Task MessageHandlerFunc(IMessageContext context, object message);


    public class TapetiConfig
    {
        private readonly Dictionary<string, List<Binding>> staticRegistrations = new Dictionary<string, List<Binding>>();
        private readonly Dictionary<Type, List<Binding>> dynamicRegistrations = new Dictionary<Type, List<Binding>>();

        private readonly List<IBindingMiddleware> bindingMiddleware = new List<IBindingMiddleware>();
        private readonly List<IMessageMiddleware> messageMiddleware = new List<IMessageMiddleware>();
        private readonly List<IPublishMiddleware> publishMiddleware = new List<IPublishMiddleware>();

        private readonly IDependencyResolver dependencyResolver;


        public TapetiConfig(IDependencyResolver dependencyResolver)
        {
            this.dependencyResolver = dependencyResolver;

            Use(new DependencyResolverBinding());
            Use(new MessageBinding());
            Use(new PublishResultBinding());
        }


        public IConfig Build()
        {
            RegisterDefaults();

            var queues = new List<IQueue>();
            queues.AddRange(staticRegistrations.Select(qb => new Queue(new QueueInfo { Dynamic = false, Name = qb.Key }, qb.Value)));

            // Group all bindings with the same index into queues, this will
            // ensure each message type is unique on their queue
            var dynamicBindings = new List<List<Binding>>();
            foreach (var bindings in dynamicRegistrations.Values)
            {
                while (dynamicBindings.Count < bindings.Count)
                    dynamicBindings.Add(new List<Binding>());

                for (var bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
                    dynamicBindings[bindingIndex].Add(bindings[bindingIndex]);
            }

            queues.AddRange(dynamicBindings.Select(bl => new Queue(new QueueInfo { Dynamic = true }, bl)));

            var config = new Config(dependencyResolver, messageMiddleware, publishMiddleware, queues);
            (dependencyResolver as IDependencyContainer)?.RegisterDefaultSingleton<IConfig>(config);

            return config;
        }


        public TapetiConfig Use(IBindingMiddleware handler)
        {
            bindingMiddleware.Add(handler);
            return this;
        }


        public TapetiConfig Use(IMessageMiddleware handler)
        {
            messageMiddleware.Add(handler);
            return this;
        }


        public TapetiConfig Use(IPublishMiddleware handler)
        {
            publishMiddleware.Add(handler);
            return this;
        }


        public TapetiConfig Use(ITapetiExtension extension)
        {
            var container = dependencyResolver as IDependencyContainer;
            if (container != null)
                extension.RegisterDefaults(container);

            var middlewareBundle = extension.GetMiddleware(dependencyResolver);

            // ReSharper disable once InvertIf
            if (middlewareBundle != null)
            {
                foreach (var middleware in middlewareBundle)
                {
                    // ReSharper disable once CanBeReplacedWithTryCastAndCheckForNull
                    if (middleware is IBindingMiddleware)
                        Use((IBindingMiddleware)middleware);
                    else if (middleware is IMessageMiddleware)
                        Use((IMessageMiddleware)middleware);
                    else if (middleware is IPublishMiddleware)
                        Use((IPublishMiddleware)middleware);
                    else
                        throw new ArgumentException($"Unsupported middleware implementation: {(middleware == null ? "null" : middleware.GetType().Name)}");
                }
            }

            return this;
        }


        public void RegisterDefaults()
        {
            var container = dependencyResolver as IDependencyContainer;
            if (container == null)
                return;

            if (ConsoleHelper.IsAvailable())
                container.RegisterDefault<ILogger, ConsoleLogger>();
            else
                container.RegisterDefault<ILogger, DevNullLogger>();

            container.RegisterDefault<IMessageSerializer, JsonMessageSerializer>();
            container.RegisterDefault<IExchangeStrategy, NamespaceMatchExchangeStrategy>();
            container.RegisterDefault<IRoutingKeyStrategy, TypeNameRoutingKeyStrategy>();
            container.RegisterDefault<IExceptionStrategy, RequeueExceptionStrategy>();
        }


        public TapetiConfig RegisterController(Type controller)
        {
            var controllerQueueInfo = GetQueueInfo(controller);

            (dependencyResolver as IDependencyContainer)?.RegisterController(controller);

            foreach (var method in controller.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.MemberType == MemberTypes.Method && m.DeclaringType != typeof(object) && !(m as MethodInfo).IsSpecialName)
                .Select(m => (MethodInfo)m))
            {
                var context = new BindingContext(method);
                var messageHandler = GetMessageHandler(context, method);
                if (messageHandler == null)
                    continue;

                var methodQueueInfo = GetQueueInfo(method) ?? controllerQueueInfo;
                if (!methodQueueInfo.IsValid)
                    throw new TopologyConfigurationException(
                        $"Method {method.Name} or controller {controller.Name} requires a queue attribute");

                var handlerInfo = new Binding
                {
                    Controller = controller,
                    Method = method,
                    QueueInfo = methodQueueInfo,
                    QueueBindingMode = context.QueueBindingMode,
                    MessageClass = context.MessageClass,
                    MessageHandler = messageHandler,
                    MessageMiddleware = context.MessageMiddleware,
                    MessageFilterMiddleware = context.MessageFilterMiddleware
                };

                if (methodQueueInfo.Dynamic.GetValueOrDefault())
                    AddDynamicRegistration(context, handlerInfo);
                else
                    AddStaticRegistration(context, handlerInfo);
            }

            return this;
        }


        public TapetiConfig RegisterAllControllers(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsDefined(typeof(MessageControllerAttribute))))
                RegisterController(type);

            return this;
        }


        public TapetiConfig RegisterAllControllers()
        {
            return RegisterAllControllers(Assembly.GetEntryAssembly());
        }


        protected MessageHandlerFunc GetMessageHandler(IBindingContext context, MethodInfo method)
        {
            var allowBinding= false;

            MiddlewareHelper.Go(bindingMiddleware, 
                (handler, next) => handler.Handle(context, next),
                () =>
                {
                    allowBinding = true;
                });

            if (!allowBinding)
                return null;

            if (context.MessageClass == null)
                throw new TopologyConfigurationException($"Method {method.Name} in controller {method.DeclaringType?.Name} does not resolve to a message class");


            var invalidBindings = context.Parameters.Where(p => !p.HasBinding).ToList();

            // ReSharper disable once InvertIf
            if (invalidBindings.Count > 0)
            {
                var parameterNames = string.Join(", ", invalidBindings.Select(p => p.Info.Name));
                throw new TopologyConfigurationException($"Method {method.Name} in controller {method.DeclaringType?.Name} has unknown parameters: {parameterNames}");
            }

            var resultHandler = ((IBindingResultAccess) context.Result).GetHandler();

            return WrapMethod(method, context.Parameters.Select(p => ((IBindingParameterAccess)p).GetBinding()), resultHandler);
        }


        protected MessageHandlerFunc WrapMethod(MethodInfo method, IEnumerable<ValueFactory> parameters, ResultHandler resultHandler)
        {
            if (resultHandler != null)
                return WrapResultHandlerMethod(method, parameters, resultHandler);

            if (method.ReturnType == typeof(void))
                return WrapNullMethod(method, parameters);

            if (method.ReturnType == typeof(Task))
                return WrapTaskMethod(method, parameters);

            if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                return WrapGenericTaskMethod(method, parameters);

            return WrapObjectMethod(method, parameters);
        }


        protected MessageHandlerFunc WrapResultHandlerMethod(MethodInfo method, IEnumerable<ValueFactory> parameters, ResultHandler resultHandler)
        {
            return (context, message) =>
            {
                var result = method.Invoke(context.Controller, parameters.Select(p => p(context)).ToArray());
                return resultHandler(context, result);
            };
        }

        protected MessageHandlerFunc WrapNullMethod(MethodInfo method, IEnumerable<ValueFactory> parameters)
        {
            return (context, message) =>
            {
                method.Invoke(context.Controller, parameters.Select(p => p(context)).ToArray());
                return Task.CompletedTask;
            };
        }


        protected MessageHandlerFunc WrapTaskMethod(MethodInfo method, IEnumerable<ValueFactory> parameters)
        {
            return (context, message) => (Task)method.Invoke(context.Controller, parameters.Select(p => p(context)).ToArray());
        }


        protected MessageHandlerFunc WrapGenericTaskMethod(MethodInfo method, IEnumerable<ValueFactory> parameters)
        {
            return (context, message) =>
            {
                return (Task<object>)method.Invoke(context.Controller, parameters.Select(p => p(context)).ToArray());
            };
        }


        protected MessageHandlerFunc WrapObjectMethod(MethodInfo method, IEnumerable<ValueFactory> parameters)
        {
            return (context, message) =>
            {
                return Task.FromResult(method.Invoke(context.Controller, parameters.Select(p => p(context)).ToArray()));
            };
        }


        protected void AddStaticRegistration(IBindingContext context, Binding binding)
        {
            if (staticRegistrations.ContainsKey(binding.QueueInfo.Name))
            {
                var existing = staticRegistrations[binding.QueueInfo.Name];

                // TODO allow multiple only if there is a filter which guarantees uniqueness? and/or move to independant validation middleware
                //if (existing.Any(h => h.MessageClass == binding.MessageClass))
                //    throw new TopologyConfigurationException($"Multiple handlers for message class {binding.MessageClass.Name} in queue {binding.QueueInfo.Name}");

                existing.Add(binding);
            }
            else
                staticRegistrations.Add(binding.QueueInfo.Name, new List<Binding> { binding });
        }


        protected void AddDynamicRegistration(IBindingContext context, Binding binding)
        {
            if (dynamicRegistrations.ContainsKey(context.MessageClass))
                dynamicRegistrations[context.MessageClass].Add(binding);
            else
                dynamicRegistrations.Add(context.MessageClass, new List<Binding> { binding });
        }


        protected QueueInfo GetQueueInfo(MemberInfo member)
        {
            var dynamicQueueAttribute = member.GetCustomAttribute<DynamicQueueAttribute>();
            var durableQueueAttribute = member.GetCustomAttribute<DurableQueueAttribute>();

            if (dynamicQueueAttribute != null && durableQueueAttribute != null)
                throw new TopologyConfigurationException($"Cannot combine static and dynamic queue attributes on {member.Name}");

            if (dynamicQueueAttribute != null)
                return new QueueInfo { Dynamic = true };

            if (durableQueueAttribute != null)
                return new QueueInfo { Dynamic = false, Name = durableQueueAttribute.Name };

            return null;
        }


        protected class QueueInfo
        {
            public bool? Dynamic { get; set; }
            public string Name { get; set; }

            public bool IsValid => Dynamic.HasValue || !string.IsNullOrEmpty(Name);
        }


        protected class Config : IConfig
        {
            public IDependencyResolver DependencyResolver { get; }
            public IReadOnlyList<IMessageMiddleware> MessageMiddleware { get; }
            public IReadOnlyList<IPublishMiddleware> PublishMiddleware { get; }
            public IEnumerable<IQueue> Queues { get; }

            private readonly Dictionary<MethodInfo, IBinding> bindingMethodLookup;


            public Config(IDependencyResolver dependencyResolver, IReadOnlyList<IMessageMiddleware> messageMiddleware, IReadOnlyList<IPublishMiddleware> publishMiddleware, IEnumerable<IQueue> queues)
            {
                DependencyResolver = dependencyResolver;
                MessageMiddleware = messageMiddleware;
                PublishMiddleware = publishMiddleware;
                Queues = queues.ToList();

                bindingMethodLookup = Queues.SelectMany(q => q.Bindings).ToDictionary(b => b.Method, b => b);
            }


            public IBinding GetBinding(Delegate method)
            {
                IBinding binding;
                return bindingMethodLookup.TryGetValue(method.Method, out binding) ? binding : null;
            }
        }


        protected class Queue : IDynamicQueue
        {
            public bool Dynamic { get; }
            public string Name { get; set;  }
            public IEnumerable<IBinding> Bindings { get; }


            public Queue(QueueInfo queue, IEnumerable<IBinding> bindings)
            {
                Dynamic = queue.Dynamic.GetValueOrDefault();
                Name = queue.Name;
                Bindings = bindings;
            }


            public void SetName(string name)
            {
                Name = name;
            }
        }


        protected class Binding : IBuildBinding
        {
            public Type Controller { get; set; }
            public MethodInfo Method { get; set; }
            public Type MessageClass { get; set; }
            public string QueueName { get; set; }
            public QueueBindingMode QueueBindingMode { get; set; }

            public IReadOnlyList<IMessageMiddleware> MessageMiddleware { get; set;  }
            public IReadOnlyList<IMessageFilterMiddleware> MessageFilterMiddleware { get; set; }

            private QueueInfo queueInfo;
            public QueueInfo QueueInfo
            {
                get { return queueInfo; }
                set
                {
                    QueueName = (value?.Dynamic).GetValueOrDefault() ? value?.Name : null;
                    queueInfo = value;
                }
            }

            public MessageHandlerFunc MessageHandler { get; set; }


            public void SetQueueName(string queueName)
            {
                QueueName = queueName;
            }


            public bool Accept(IMessageContext context, object message)
            {
                return message.GetType() == MessageClass;
            }


            public Task Invoke(IMessageContext context, object message)
            {
                return MessageHandler(context, message);
            }
        }


        internal interface IBindingParameterAccess
        {
            ValueFactory GetBinding();
        }



        internal interface IBindingResultAccess
        {
            ResultHandler GetHandler();
        }


        internal class BindingContext : IBindingContext
        {
            private List<IMessageMiddleware> messageMiddleware;
            private List<IMessageFilterMiddleware> messageFilterMiddleware;

            public Type MessageClass { get; set; }

            public MethodInfo Method { get; }
            public IReadOnlyList<IBindingParameter> Parameters { get; }
            public IBindingResult Result { get; }

            public QueueBindingMode QueueBindingMode { get; set; }

            public IReadOnlyList<IMessageMiddleware> MessageMiddleware => messageMiddleware;
            public IReadOnlyList<IMessageFilterMiddleware> MessageFilterMiddleware => messageFilterMiddleware;


            public BindingContext(MethodInfo method)
            {
                Method = method;
                
                Parameters = method.GetParameters().Select(p => new BindingParameter(p)).ToList();
                Result = new BindingResult(method.ReturnParameter);
            }


            public void Use(IMessageMiddleware middleware)
            {
                if (messageMiddleware == null)
                    messageMiddleware = new List<IMessageMiddleware>();

                messageMiddleware.Add(middleware);
            }


            public void Use(IMessageFilterMiddleware filterMiddleware)
            {
                if (messageFilterMiddleware == null)
                    messageFilterMiddleware = new List<IMessageFilterMiddleware>();

                messageFilterMiddleware.Add(filterMiddleware);
            }
        }


        internal class BindingParameter : IBindingParameter, IBindingParameterAccess
        {
            private ValueFactory binding;

            public ParameterInfo Info { get; }
            public bool HasBinding => binding != null;


            public BindingParameter(ParameterInfo parameter)
            {
                Info = parameter;
                
            }

            public ValueFactory GetBinding()
            {
                return binding;
            }

            public void SetBinding(ValueFactory valueFactory)
            {
                binding = valueFactory;
            }
        }


        internal class BindingResult : IBindingResult, IBindingResultAccess
        {
            private ResultHandler handler;

            public ParameterInfo Info { get; }
            public bool HasHandler => handler != null;


            public BindingResult(ParameterInfo parameter)
            {
                Info = parameter;
            }


            public ResultHandler GetHandler()
            {
                return handler;
            }

            public void SetHandler(ResultHandler resultHandler)
            {
                handler = resultHandler;
            }
        }
    }
}
