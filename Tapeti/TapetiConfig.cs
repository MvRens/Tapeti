using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tapeti.Config;
using Tapeti.Default;
using Tapeti.Helpers;

// ReSharper disable UnusedMember.Global

namespace Tapeti
{
    /// <inheritdoc cref="ITapetiConfigBuilder" />
    /// <summary>
    /// Default implementation of the Tapeti config builder.
    /// Automatically registers the default middleware for injecting the message parameter and handling the return value.
    /// </summary>
    public class TapetiConfig : ITapetiConfigBuilder, ITapetiConfigBuilderAccess
    {
        private Config config;
        private readonly List<IControllerBindingMiddleware> bindingMiddleware = new List<IControllerBindingMiddleware>();


        /// <inheritdoc />
        public IDependencyResolver DependencyResolver => GetConfig().DependencyResolver;


        /// <summary>
        /// Instantiates a new Tapeti config builder.
        /// </summary>
        /// <param name="dependencyResolver">A wrapper implementation for an IoC container to allow dependency injection</param>
        public TapetiConfig(IDependencyResolver dependencyResolver)
        {
            config = new Config(dependencyResolver);
        }


        /// <inheritdoc />
        public ITapetiConfig Build()
        {
            if (config == null)
                throw new InvalidOperationException("TapetiConfig.Build must only be called once");

            Use(new DependencyResolverBinding());
            Use(new PublishResultBinding());

            // Registered last so it runs first and the MessageClass is known to other middleware
            Use(new MessageBinding());

            RegisterDefaults();
            (config.DependencyResolver as IDependencyContainer)?.RegisterDefaultSingleton<ITapetiConfig>(config);


            var outputConfig = config;
            config = null;

            outputConfig.Lock();
            return outputConfig;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder Use(IControllerBindingMiddleware handler)
        {
            bindingMiddleware.Add(handler);
            return this;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder Use(IMessageMiddleware handler)
        {
            GetConfig().Use(handler);
            return this;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder Use(IPublishMiddleware handler)
        {
            GetConfig().Use(handler);
            return this;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder Use(ITapetiExtension extension)
        {
            if (DependencyResolver is IDependencyContainer container)
                extension.RegisterDefaults(container);

            var configInstance = GetConfig();

            var middlewareBundle = extension.GetMiddleware(DependencyResolver);
            if (middlewareBundle != null)
            {
                foreach (var middleware in middlewareBundle)
                {
                    switch (middleware)
                    {
                        case IControllerBindingMiddleware bindingExtension:
                            Use(bindingExtension);
                            break;

                        case IMessageMiddleware messageExtension:
                            configInstance.Use(messageExtension);
                            break;

                        case IPublishMiddleware publishExtension:
                            configInstance.Use(publishExtension);
                            break;

                        default:
                            throw new ArgumentException(
                                $"Unsupported middleware implementation: {(middleware == null ? "null" : middleware.GetType().Name)}");
                    }
                }
            }

            var bindingBundle = (extension as ITapetiExtensionBinding)?.GetBindings(DependencyResolver);
            if (bindingBundle == null)
                return this;

            foreach (var binding in bindingBundle)
                config.RegisterBinding(binding);

            return this;
        }


        /// <inheritdoc />
        public void RegisterBinding(IBinding binding)
        {
            GetConfig().RegisterBinding(binding);
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder DisablePublisherConfirms()
        {
            GetConfig().SetPublisherConfirms(false);
            return this;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder SetPublisherConfirms(bool enabled)
        {
            GetConfig().SetPublisherConfirms(enabled);
            return this;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder EnableDeclareDurableQueues()
        {
            GetConfig().SetDeclareDurableQueues(true);
            return this;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder SetDeclareDurableQueues(bool enabled)
        {
            GetConfig().SetDeclareDurableQueues(enabled);
            return this;
        }


        /// <summary>
        /// Registers the default implementation of various Tapeti interfaces into the IoC container.
        /// </summary>
        protected void RegisterDefaults()
        {
            if (!(DependencyResolver is IDependencyContainer container))
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


        /// <inheritdoc />
        public void ApplyBindingMiddleware(IControllerBindingContext context, Action lastHandler)
        {
            MiddlewareHelper.Go(bindingMiddleware,
                (handler, next) => handler.Handle(context, next),
                lastHandler);
        }


        private Config GetConfig()
        {
            if (config == null)
                throw new InvalidOperationException("TapetiConfig can not be updated after Build");

            return null;
        }


        /// <inheritdoc />
        internal class Config : ITapetiConfig
        {
            private readonly ConfigFeatures features = new ConfigFeatures();
            private readonly ConfigMiddleware middleware = new ConfigMiddleware();
            private readonly ConfigBindings bindings = new ConfigBindings();

            public IDependencyResolver DependencyResolver { get; }
            public ITapetiConfigFeatues Features => features;
            public ITapetiConfigMiddleware Middleware => middleware;
            public ITapetiConfigBindings Bindings => bindings;


            public Config(IDependencyResolver dependencyResolver)
            {
                DependencyResolver = dependencyResolver;
            }


            public void Lock()
            {
                bindings.Lock();
            }


            public void Use(IMessageMiddleware handler)
            {
                middleware.Use(handler);
            }

            public void Use(IPublishMiddleware handler)
            {
                middleware.Use(handler);
            }


            public void RegisterBinding(IBinding binding)
            {
                bindings.Add(binding);
            }


            public void SetPublisherConfirms(bool enabled)
            {
                features.PublisherConfirms = enabled;
            }

            public void SetDeclareDurableQueues(bool enabled)
            {
                features.DeclareDurableQueues = enabled;
            }
        }


        internal class ConfigFeatures : ITapetiConfigFeatues
        {
            public bool PublisherConfirms { get; internal set; } = true;
            public bool DeclareDurableQueues { get; internal set; } = true;
        }


        internal class ConfigMiddleware : ITapetiConfigMiddleware
        {
            private readonly List<IMessageMiddleware> messageMiddleware = new List<IMessageMiddleware>();
            private readonly List<IPublishMiddleware> publishMiddleware = new List<IPublishMiddleware>();


            public IReadOnlyList<IMessageMiddleware> Message => messageMiddleware;
            public IReadOnlyList<IPublishMiddleware> Publish => publishMiddleware;


            public void Use(IMessageMiddleware handler)
            {
                messageMiddleware.Add(handler);
            }

            public void Use(IPublishMiddleware handler)
            {
                publishMiddleware.Add(handler);
            }
        }


        internal class ConfigBindings : List<IBinding>, ITapetiConfigBindings
        {
            private Dictionary<MethodInfo, IControllerMethodBinding> methodLookup;


            public IControllerMethodBinding ForMethod(Delegate method)
            {
                return methodLookup.TryGetValue(method.Method, out var binding) ? binding : null;
            }


            public void Lock()
            {
                methodLookup = this
                    .Where(binding => binding is IControllerMethodBinding)
                    .Cast<IControllerMethodBinding>()
                    .ToDictionary(binding => binding.Method, binding => binding);
            }
        }
    }


    /*
    public delegate Task MessageHandlerFunc(IMessageContext context, object message);


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


        protected void AddStaticRegistration(IBindingQueueInfo binding)
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


        protected string GetDynamicQueueName(string prefix)
        {
            if (String.IsNullOrEmpty(prefix))
                return "";

            return prefix + "." + Guid.NewGuid().ToString("N");
        }
    }
    */
}
