using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Annotations;
using Tapeti.Config;
using Tapeti.Default;
using Tapeti.Helpers;

// ReSharper disable UnusedMember.Global

namespace Tapeti
{
    public class TopologyConfigurationException : Exception
    {
        public TopologyConfigurationException(string message) : base(message) { }
    }

    public delegate Task MessageHandlerFunc(IMessageContext context, object message);


    public class TapetiConfig
    {
        private readonly Dictionary<string, List<IBinding>> staticRegistrations = new Dictionary<string, List<IBinding>>();
        private readonly Dictionary<string, Dictionary<Type, List<IBinding>>> dynamicRegistrations = new Dictionary<string, Dictionary<Type, List<IBinding>>>();

        private readonly List<IBindingMiddleware> bindingMiddleware = new List<IBindingMiddleware>();
        private readonly List<IMessageMiddleware> messageMiddleware = new List<IMessageMiddleware>();
        private readonly List<ICleanupMiddleware> cleanupMiddleware = new List<ICleanupMiddleware>();
        private readonly List<IPublishMiddleware> publishMiddleware = new List<IPublishMiddleware>();

        private readonly IDependencyResolver dependencyResolver;

        private bool usePublisherConfirms = true;


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


            // We want to ensure each queue only has unique messages classes. This means we can requeue
            // without the side-effect of calling other handlers for the same message class again as well.
            //
            // Since I had trouble deciphering this code after a year, here's an overview of how it achieves this grouping
            // and how the bindingIndex is relevant:
            // 
            // dynamicRegistrations:
            //   Key (prefix)
            //     ""
            //       Key (message class)    Value (list of bindings)
            //       A                      binding1, binding2, binding3
            //       B                      binding4
            //     "prefix"
            //       A                      binding5, binding6
            //
            // By combining all bindings with the same index, per prefix, the following queues will be registered:
            //
            // Prefix       Bindings
            // ""           binding1 (message A), binding4 (message B)
            // ""           binding2 (message A)
            // ""           binding3 (message A)
            // "prefix"     binding5 (message A)
            // "prefix"     binding6 (message A)
            //
            foreach (var prefixGroup in dynamicRegistrations)
            {
                var dynamicBindings = new List<List<IBinding>>();

                foreach (var bindings in prefixGroup.Value.Values)
                {
                    while (dynamicBindings.Count < bindings.Count)
                        dynamicBindings.Add(new List<IBinding>());

                    for (var bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
                        dynamicBindings[bindingIndex].Add(bindings[bindingIndex]);
                }

                queues.AddRange(dynamicBindings.Select(bl => new Queue(new QueueInfo { Dynamic = true, Name = GetDynamicQueueName(prefixGroup.Key) }, bl)));
            }

            var config = new Config(queues)
            {
                DependencyResolver = dependencyResolver, 
                MessageMiddleware = messageMiddleware, 
                CleanupMiddleware = cleanupMiddleware, 
                PublishMiddleware = publishMiddleware,

                UsePublisherConfirms = usePublisherConfirms
            };

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


        public TapetiConfig Use(ICleanupMiddleware handler)
        {
            cleanupMiddleware.Add(handler);
            return this;
        }


        public TapetiConfig Use(IPublishMiddleware handler)
        {
            publishMiddleware.Add(handler);
            return this;
        }


        public TapetiConfig Use(ITapetiExtension extension)
        {
            if (dependencyResolver is IDependencyContainer container)
                extension.RegisterDefaults(container);

            var middlewareBundle = extension.GetMiddleware(dependencyResolver);

            (extension as ITapetiExtentionBinding)?.GetBindings(dependencyResolver);

            // ReSharper disable once InvertIf
            if (middlewareBundle != null)
            {
                foreach (var middleware in middlewareBundle)
                {
                    // ReSharper disable once CanBeReplacedWithTryCastAndCheckForNull
                    if (middleware is IBindingMiddleware bindingExtension)
                        Use(bindingExtension);
                    else if (middleware is IMessageMiddleware messageExtension)
                        Use(messageExtension);
                    else if (middleware is ICleanupMiddleware cleanupExtension)
                        Use(cleanupExtension);
                    else if (middleware is IPublishMiddleware publishExtension)
                        Use(publishExtension);
                    else
                        throw new ArgumentException($"Unsupported middleware implementation: {(middleware == null ? "null" : middleware.GetType().Name)}");
                }
            }

            return this;
        }

        
        /// <summary>
        /// WARNING: disabling publisher confirms means there is no guarantee that a Publish succeeds,
        /// and disables Tapeti.Flow from verifying if a request/response can be routed. This may
        /// result in never-ending flows. Only disable if you can accept those consequences.
        /// </summary>
        public TapetiConfig DisablePublisherConfirms()
        {
            usePublisherConfirms = false;
            return this;
        }


        /// <summary>
        /// WARNING: disabling publisher confirms means there is no guarantee that a Publish succeeds,
        /// and disables Tapeti.Flow from verifying if a request/response can be routed. This may
        /// result in never-ending flows. Only disable if you accept those consequences.
        /// </summary>
        public TapetiConfig SetPublisherConfirms(bool enabled)
        {
            usePublisherConfirms = enabled;
            return this;
        }


        public void RegisterDefaults()
        {
            if (!(dependencyResolver is IDependencyContainer container))
                return;

            if (ConsoleHelper.IsAvailable())
                container.RegisterDefault<ILogger, ConsoleLogger>();
            else
                container.RegisterDefault<ILogger, DevNullLogger>();

            container.RegisterDefault<IMessageSerializer, JsonMessageSerializer>();
            container.RegisterDefault<IExchangeStrategy, NamespaceMatchExchangeStrategy>();
            container.RegisterDefault<IRoutingKeyStrategy, TypeNameRoutingKeyStrategy>();
            container.RegisterDefault<IExceptionStrategy, NackExceptionStrategy>();
        }


        public TapetiConfig RegisterController(Type controller)
        {
            var controllerQueueInfo = GetQueueInfo(controller);

            if (!controller.IsInterface)
                (dependencyResolver as IDependencyContainer)?.RegisterController(controller);

            foreach (var method in controller.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.MemberType == MemberTypes.Method && m.DeclaringType != typeof(object) && (m as MethodInfo)?.IsSpecialName == false)
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


        protected void AddStaticRegistration(IBindingContext context, IBindingQueueInfo binding)
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
                staticRegistrations.Add(binding.QueueInfo.Name, new List<IBinding> { binding });
        }


        protected void AddDynamicRegistration(IBindingContext context, IBindingQueueInfo binding)
        {
            var prefix = binding.QueueInfo.Name ?? "";

            if (!dynamicRegistrations.TryGetValue(prefix, out Dictionary<Type, List<IBinding>> prefixRegistrations))
            {
                prefixRegistrations = new Dictionary<Type, List<IBinding>>();
                dynamicRegistrations.Add(prefix, prefixRegistrations);
            }

            if (!prefixRegistrations.TryGetValue(context.MessageClass, out List<IBinding> bindings))
            {
                bindings = new List<IBinding>();
                prefixRegistrations.Add(context.MessageClass, bindings);
            }

            bindings.Add(binding);
        }


        protected QueueInfo GetQueueInfo(MemberInfo member)
        {
            var dynamicQueueAttribute = member.GetCustomAttribute<DynamicQueueAttribute>();
            var durableQueueAttribute = member.GetCustomAttribute<DurableQueueAttribute>();

            if (dynamicQueueAttribute != null && durableQueueAttribute != null)
                throw new TopologyConfigurationException($"Cannot combine static and dynamic queue attributes on {member.Name}");

            if (dynamicQueueAttribute != null)
                return new QueueInfo { Dynamic = true, Name = dynamicQueueAttribute.Prefix };

            if (durableQueueAttribute != null)
                return new QueueInfo { Dynamic = false, Name = durableQueueAttribute.Name };

            return null;
        }


        protected string GetDynamicQueueName(string prefix)
        {
            if (String.IsNullOrEmpty(prefix))
                return "";

            return prefix + "." + Guid.NewGuid().ToString("N");
        }


        protected class QueueInfo
        {
            public bool? Dynamic { get; set; }
            public string Name { get; set; }

            public bool IsValid => Dynamic.HasValue || !string.IsNullOrEmpty(Name);
        }


        protected class Config : IConfig
        {
            public bool UsePublisherConfirms { get; set; }

            public IDependencyResolver DependencyResolver { get; set; }
            public IReadOnlyList<IMessageMiddleware> MessageMiddleware { get; set; }
            public IReadOnlyList<ICleanupMiddleware> CleanupMiddleware { get; set; }
            public IReadOnlyList<IPublishMiddleware> PublishMiddleware { get; set; }
            public IEnumerable<IQueue> Queues { get; }

            private readonly Dictionary<MethodInfo, IBinding> bindingMethodLookup;


            public Config(IEnumerable<IQueue> queues)
            {
                Queues = queues.ToList();

                bindingMethodLookup = Queues.SelectMany(q => q.Bindings).ToDictionary(b => b.Method, b => b);
            }


            public IBinding GetBinding(Delegate method)
            {
                return bindingMethodLookup.TryGetValue(method.Method, out var binding) ? binding : null;
            }
        }


        protected class Queue : IDynamicQueue
        {
            private readonly string declareQueueName;

            public bool Dynamic { get; }
            public string Name { get; set;  }
            public IEnumerable<IBinding> Bindings { get; }


            public Queue(QueueInfo queue, IEnumerable<IBinding> bindings)
            {
                declareQueueName = queue.Name;

                Dynamic = queue.Dynamic.GetValueOrDefault();
                Name = queue.Name;
                Bindings = bindings;
            }


            public string GetDeclareQueueName()
            {
                return declareQueueName;
            }


            public void SetName(string name)
            {
                Name = name;
            }
        }

        protected interface IBindingQueueInfo : IBuildBinding
        {
            QueueInfo QueueInfo { get; }
        }

        protected class Binding : IBindingQueueInfo
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
                get => queueInfo;
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


            public bool Accept(Type messageClass)
            {
                return MessageClass.IsAssignableFrom(messageClass);
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


        protected class CustomBinding : IBindingQueueInfo
        {
            private readonly ICustomBinding inner;

            public CustomBinding(ICustomBinding inner)
            {
                this.inner = inner;

                // Copy all variables to make them guaranteed readonly.
                Controller = inner.Controller;
                Method = inner.Method;
                QueueBindingMode = inner.QueueBindingMode;
                MessageClass = inner.MessageClass;

                QueueInfo = inner.StaticQueueName != null
                    ? new QueueInfo()
                    {
                        Dynamic = false,
                        Name = inner.StaticQueueName
                    }
                    : new QueueInfo()
                    {
                        Dynamic = true,
                        Name = inner.DynamicQueuePrefix
                    };

                // Custom bindings cannot have other middleware messing with the binding.
                MessageFilterMiddleware = new IMessageFilterMiddleware[0];
                MessageMiddleware = new IMessageMiddleware[0];
            }

            public Type Controller { get; }
            public MethodInfo Method { get; }
            public string QueueName { get; private set; }
            public QueueBindingMode QueueBindingMode { get; set; }
            public IReadOnlyList<IMessageFilterMiddleware> MessageFilterMiddleware { get; }
            public IReadOnlyList<IMessageMiddleware> MessageMiddleware { get; }

            public bool Accept(Type messageClass)
            {
                return inner.Accept(messageClass);
            }

            public bool Accept(IMessageContext context, object message)
            {
                return inner.Accept(context, message);
            }

            public Task Invoke(IMessageContext context, object message)
            {
                return inner.Invoke(context, message);
            }

            public void SetQueueName(string queueName)
            {
                QueueName = queueName;
                inner.SetQueueName(queueName);
            }

            public Type MessageClass { get; }
            public QueueInfo QueueInfo { get; }
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
