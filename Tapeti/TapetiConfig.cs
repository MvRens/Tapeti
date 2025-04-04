﻿using System;
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
    public class TapetiConfig : ITapetiConfigBuilderAccess
    {
        private Config? config;
        private readonly List<IControllerBindingMiddleware> bindingMiddleware = new();


        /// <inheritdoc />
        public IDependencyResolver DependencyResolver => GetConfig().DependencyResolver;


        /// <summary>
        /// Instantiates a new Tapeti config builder.
        /// </summary>
        /// <param name="dependencyResolver">A wrapper implementation for an IoC container to allow dependency injection</param>
        public TapetiConfig(IDependencyResolver dependencyResolver)
        {
            config = new Config(dependencyResolver);

            Use(new DependencyResolverBinding());
            Use(new PublishResultBinding());
            Use(new CancellationTokenBinding());

            // Registered last so it runs first and the MessageClass is known to other middleware
            Use(new MessageBinding());
        }


        /// <inheritdoc />
        public ITapetiConfig Build()
        {
            if (config == null)
                throw new InvalidOperationException("TapetiConfig.Build must only be called once");

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

            foreach (var middleware in extension.GetMiddleware(DependencyResolver))
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
                            $"Unsupported middleware implementation: {middleware.GetType().Name}");
                }
            }

            var bindingBundle = (extension as ITapetiExtensionBinding)?.GetBindings(DependencyResolver);
            if (bindingBundle == null)
                return this;

            foreach (var binding in bindingBundle)
                GetConfig().RegisterBinding(binding);

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
            GetConfig().GetFeaturesBuilder().DisablePublisherConfirms();
            return this;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder SetPublisherConfirms(bool enabled)
        {
            GetConfig().GetFeaturesBuilder().SetPublisherConfirms(enabled);
            return this;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder EnableDeclareDurableQueues()
        {
            GetConfig().GetFeaturesBuilder().EnableDeclareDurableQueues();
            return this;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder SetDeclareDurableQueues(bool enabled)
        {
            GetConfig().GetFeaturesBuilder().SetDeclareDurableQueues(enabled);
            return this;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder DisableVerifyDurableQueues()
        {
            GetConfig().GetFeaturesBuilder().DisablePublisherConfirms();
            return this;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder SetVerifyDurableQueues(bool enabled)
        {
            GetConfig().GetFeaturesBuilder().SetVerifyDurableQueues(enabled);
            return this;
        }


        /// <inheritdoc />
        public ITapetiConfigBuilder DelayFeatures(Action<ITapetiConfigFeaturesBuilder> onBuild)
        {
            GetConfig().GetFeaturesBuilder().DelayFeatures(onBuild);
            return this;
        }


        /// <summary>
        /// Registers the default implementation of various Tapeti interfaces into the IoC container.
        /// </summary>
        protected void RegisterDefaults()
        {
            if (DependencyResolver is not IDependencyContainer container)
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

            return config;
        }


        /// <inheritdoc />
        internal class Config : ITapetiConfig
        {
            private ConfigFeaturesBuilder? featuresBuilder = new();
            private ITapetiConfigFeatures? features;

            private readonly ConfigMiddleware middleware = new();
            private readonly ConfigBindings bindings = new();

            public IDependencyResolver DependencyResolver { get; }
            public ITapetiConfigMiddleware Middleware => middleware;
            public ITapetiConfigBindings Bindings => bindings;


            public Config(IDependencyResolver dependencyResolver)
            {
                DependencyResolver = dependencyResolver;
            }


            public ITapetiConfigFeatures GetFeatures()
            {
                if (features != null)
                    return features;

                features = featuresBuilder!.Build();
                featuresBuilder = null;
                return features;
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


            internal ConfigFeaturesBuilder GetFeaturesBuilder()
            {
                if (featuresBuilder == null)
                    throw new InvalidOperationException("Tapeti features are already frozen");

                return featuresBuilder;
            }
        }


        internal class ConfigFeatures : ITapetiConfigFeatures
        {
            public bool PublisherConfirms { get; internal set; } = true;
            public bool DeclareDurableQueues { get; internal set; }
            public bool VerifyDurableQueues { get; internal set; } = true;
        }


        internal class ConfigFeaturesBuilder : ITapetiConfigFeaturesBuilder
        {
            private bool publisherConfirms = true;
            private bool declareDurableQueues;
            private bool verifyDurableQueues = true;
            private Action<ITapetiConfigFeaturesBuilder>? onBuild;


            public ITapetiConfigFeaturesBuilder DisablePublisherConfirms()
            {
                return SetPublisherConfirms(false);
            }

            public ITapetiConfigFeaturesBuilder SetPublisherConfirms(bool enabled)
            {
                publisherConfirms = enabled;
                return this;
            }

            public ITapetiConfigFeaturesBuilder EnableDeclareDurableQueues()
            {
                return SetDeclareDurableQueues(true);
            }

            public ITapetiConfigFeaturesBuilder SetDeclareDurableQueues(bool enabled)
            {
                declareDurableQueues = enabled;
                return this;
            }

            public ITapetiConfigFeaturesBuilder DisableVerifyDurableQueues()
            {
                return SetVerifyDurableQueues(false);
            }

            public ITapetiConfigFeaturesBuilder SetVerifyDurableQueues(bool enabled)
            {
                verifyDurableQueues = enabled;
                return this;
            }

            // ReSharper disable once ParameterHidesMember
            public void DelayFeatures(Action<ITapetiConfigFeaturesBuilder> onBuild)
            {
                this.onBuild = onBuild;
            }

            public ITapetiConfigFeatures Build()
            {
                onBuild?.Invoke(this);
                return new ConfigFeatures
                {
                    DeclareDurableQueues = declareDurableQueues,
                    PublisherConfirms = publisherConfirms,
                    VerifyDurableQueues = verifyDurableQueues
                };
            }
        }


        internal class ConfigMiddleware : ITapetiConfigMiddleware
        {
            private readonly List<IMessageMiddleware> messageMiddleware = new();
            private readonly List<IPublishMiddleware> publishMiddleware = new();


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
            private Dictionary<MethodInfo, IControllerMethodBinding>? methodLookup;


            public IControllerMethodBinding? ForMethod(Delegate method)
            {
                if (methodLookup == null)
                    throw new InvalidOperationException("Lock must be called first");

                return methodLookup.TryGetValue(method.Method, out var binding) ? binding : null;
            }


            public IControllerMethodBinding? ForMethod(MethodInfo method)
            {
                if (methodLookup == null)
                    throw new InvalidOperationException("Lock must be called first");

                return methodLookup.TryGetValue(method, out var binding) ? binding : null;
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
}
