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


        /// <summary>
        /// Stores a key-value pair in the context for passing information between the various
        /// controller middleware stages (IControllerMiddlewareBase descendants).
        /// </summary>
        /// <param name="key">A unique key. It is recommended to prefix it with the package name which hosts the middleware to prevent conflicts</param>
        /// <param name="value">Will be disposed if the value implements IDisposable</param>
        void Store(string key, object value);

        /// <summary>
        /// Retrieves a previously stored value.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>True if the value was found, False otherwise</returns>
        bool Get<T>(string key, out T value) where T : class;
    }
}
