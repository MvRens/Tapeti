namespace Tapeti.Config
{
    /// <inheritdoc />
    /// <summary>
    /// Extends the message context with information about the controller.
    /// </summary>
    public class ControllerMessageContextPayload : IMessageContextPayload
    {
        /// <summary>
        /// An instance of the controller referenced by the binding. Note: can be null during Cleanup or when bound to static methods.
        /// </summary>
        public object Controller { get; }


        /// <remarks>
        /// Provides access to the binding which is currently processing the message.
        /// </remarks>
        public IControllerMethodBinding Binding { get; }


        /// <summary>
        /// Constructs the payload to enrich the message context with information about the controller.
        /// </summary>
        /// <param name="controller">An instance of the controller referenced by the binding</param>
        /// <param name="binding">The binding which is currently processing the message</param>
        public ControllerMessageContextPayload(object controller, IControllerMethodBinding binding)
        {
            Controller = controller;
            Binding = binding;
        }
    }
}
