using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tapeti.Config
{
    /// <summary>
    /// Provides access to the Tapeti configuration.
    /// </summary>
    public interface ITapetiConfig
    {
        /// <summary>
        /// Reference to the wrapper for an IoC container, to provide dependency injection to Tapeti.
        /// </summary>
        IDependencyResolver DependencyResolver { get; }

        /// <summary>
        /// Various Tapeti features which can be turned on or off.
        /// </summary>
        ITapetiConfigFeatues Features { get; }

        /// <summary>
        /// Provides access to the different kinds of registered middleware.
        /// </summary>
        ITapetiConfigMiddleware Middleware { get; }

        /// <summary>
        /// A list of all registered bindings.
        /// </summary>
        ITapetiConfigBindings Bindings { get; }
    }


    /// <summary>
    /// Various Tapeti features which can be turned on or off.
    /// </summary>
    public interface ITapetiConfigFeatues
    {
        /// <summary>
        /// Determines whether 'publisher confirms' are used. This RabbitMQ features allows Tapeti to
        /// be notified if a message has no route, and guarantees delivery for request-response style
        /// messages and those marked with the Mandatory attribute. On by default, can only be turned
        /// off by explicitly calling DisablePublisherConfirms, which is not recommended.
        /// </summary>
        bool PublisherConfirms { get; }

        /// <summary>
        /// If enabled, durable queues will be created at startup and their bindings will be updated
        /// with the currently registered message handlers. If not enabled all durable queues must
        /// already be present when the connection is made.
        /// </summary>
        bool DeclareDurableQueues { get; }


        /// <summary>
        /// Determines if durable queues are verified at startup if DeclareDurableQueues is disabled.
        /// Defaults to true.
        /// </summary>
        bool VerifyDurableQueues { get; }
    }


    /// <summary>
    /// Provides access to the different kinds of registered middleware.
    /// </summary>
    public interface ITapetiConfigMiddleware
    {
        /// <summary>
        /// A list of message middleware which is called when a message is being consumed.
        /// </summary>
        IReadOnlyList<IMessageMiddleware> Message { get; }

        /// <summary>
        /// A list of publish middleware which is called when a message is being published.
        /// </summary>
        IReadOnlyList<IPublishMiddleware> Publish { get; }
    }


    /// <summary>
    /// Contains a list of registered bindings, with a few added helpers.
    /// </summary>
    public interface ITapetiConfigBindings : IReadOnlyList<IBinding>
    {
        /// <summary>
        /// Searches for a binding linked to the specified method.
        /// </summary>
        /// <param name="method"></param>
        /// <returns>The binding if found, null otherwise</returns>
        IControllerMethodBinding? ForMethod(Delegate method);

        /// <summary>
        /// Searches for a binding linked to the specified method.
        /// </summary>
        /// <param name="method"></param>
        /// <returns>The binding if found, null otherwise</returns>
        IControllerMethodBinding? ForMethod(MethodInfo method);
    }
}
