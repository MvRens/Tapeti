using System;

namespace Tapeti.Config
{
    /// <summary>
    /// Configures Tapeti. Every method other than Build returns the builder instance
    /// for method chaining.
    /// </summary>
    public interface ITapetiConfigBuilder
    {
        /// <summary>
        /// Returns a locked version of the configuration which can be used to establish a connection.
        /// </summary>
        ITapetiConfig Build();


        /// <summary>
        /// Registers binding middleware which is called when a binding is created for a controller method.
        /// </summary>
        /// <param name="handler"></param>
        ITapetiConfigBuilder Use(IControllerBindingMiddleware handler);

        /// <summary>
        /// Registers message middleware which is called to handle an incoming message.
        /// </summary>
        /// <param name="handler"></param>
        ITapetiConfigBuilder Use(IMessageMiddleware handler);

        /// <summary>
        /// Registers publish middleware which is called when a message is published.
        /// </summary>
        /// <param name="handler"></param>
        ITapetiConfigBuilder Use(IPublishMiddleware handler);


        /// <summary>
        /// Registers a Tapeti extension, which is a bundling mechanism for features that require more than one middleware and
        /// optionally other dependency injected implementations.
        /// </summary>
        /// <param name="extension"></param>
        ITapetiConfigBuilder Use(ITapetiExtension extension);


        /// <summary>
        /// Registers a binding which can accept messages. In most cases this method should not be called outside
        /// of Tapeti. Instead use the RegisterAllControllers extension method to automatically create bindings.
        /// </summary>
        /// <param name="binding"></param>
        void RegisterBinding(IBinding binding);


        /// <summary>
        /// Disables 'publisher confirms'. This RabbitMQ features allows Tapeti to be notified if a message
        /// has no route, and guarantees delivery for request-response style messages and those marked with
        /// the Mandatory attribute. On by default.
        /// 
        /// WARNING: disabling publisher confirms means there is no guarantee that a Publish succeeds,
        /// and disables Tapeti.Flow from verifying if a request/response can be routed. This may
        /// result in never-ending flows. Only disable if you can accept those consequences.
        /// </summary>
        ITapetiConfigBuilder DisablePublisherConfirms();


        /// <summary>
        /// Configures 'publisher confirms'. This RabbitMQ features allows Tapeti to be notified if a message
        /// has no route, and guarantees delivery for request-response style messages and those marked with
        /// the Mandatory attribute. On by default.
        /// 
        /// WARNING: disabling publisher confirms means there is no guarantee that a Publish succeeds,
        /// and disables Tapeti.Flow from verifying if a request/response can be routed. This may
        /// result in never-ending flows. Only disable if you can accept those consequences.
        /// </summary>
        ITapetiConfigBuilder SetPublisherConfirms(bool enabled);


        /// <summary>
        /// Enables the automatic creation of durable queues and updating of their bindings.
        /// </summary>
        /// <remarks>
        /// Note that access to the RabbitMQ Management plugin's REST API is required for this
        /// feature to work, since AMQP does not provide a way to query existing bindings.
        /// </remarks>
        ITapetiConfigBuilder EnableDeclareDurableQueues();


        /// <summary>
        /// Configures the automatic creation of durable queues and updating of their bindings.
        /// </summary>
        /// <remarks>
        /// Note that access to the RabbitMQ Management plugin's REST API is required for this
        /// feature to work, since AMQP does not provide a way to query existing bindings.
        /// </remarks>
        ITapetiConfigBuilder SetDeclareDurableQueues(bool enabled);
    }


    /// <summary>
    /// Access interface for ITapetiConfigBuilder extension methods. Allows access to the registered middleware
    /// before the configuration is built. Implementations of ITapetiConfigBuilder should also implement this interface.
    /// Should not be used outside of Tapeti packages.
    /// </summary>
    public interface ITapetiConfigBuilderAccess
    {
        /// <summary>
        /// Provides access to the dependency resolver.
        /// </summary>
        IDependencyResolver DependencyResolver { get; }

        /// <summary>
        /// Applies the currently registered binding middleware to the specified context.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="lastHandler"></param>
        void ApplyBindingMiddleware(IControllerBindingContext context, Action lastHandler);
    }
}
