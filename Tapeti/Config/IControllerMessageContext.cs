namespace Tapeti.Config
{
    /// <inheritdoc />
    /// <summary>
    /// Extends the message context with information about the controller.
    /// </summary>
    public interface IControllerMessageContext : IMessageContext
    {
        /// <summary>
        /// An instance of the controller referenced by the binding.
        /// </summary>
        object Controller { get; }


        /// <remarks>
        /// Provides access to the binding which is currently processing the message.
        /// </remarks>
        new IControllerMethodBinding Binding { get; }
    }
}
