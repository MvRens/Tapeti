using Tapeti.Config;

namespace Tapeti.Default
{
    internal class ControllerMessageContext : IControllerMessageContext
    {
        private readonly IMessageContext decoratedContext;

        /// <inheritdoc />
        public object Controller { get; set; }

        /// <inheritdoc />
        public ITapetiConfig Config => decoratedContext.Config;

        /// <inheritdoc />
        public string Queue => decoratedContext.Queue;

        /// <inheritdoc />
        public string Exchange => decoratedContext.Exchange;

        /// <inheritdoc />
        public string RoutingKey => decoratedContext.RoutingKey;

        /// <inheritdoc />
        public object Message => decoratedContext.Message;

        /// <inheritdoc />
        public IMessageProperties Properties => decoratedContext.Properties;


        IBinding IMessageContext.Binding => decoratedContext.Binding;
        IControllerMethodBinding IControllerMessageContext.Binding => decoratedContext.Binding as IControllerMethodBinding;


        /// <inheritdoc />
        public ControllerMessageContext(IMessageContext decoratedContext)
        {
            this.decoratedContext = decoratedContext;
        }


        /// <inheritdoc />
        public void Dispose()
        {
        }


        /// <inheritdoc />
        public void Store(string key, object value)
        {
            decoratedContext.Store(key, value);
        }


        /// <inheritdoc />
        public bool Get<T>(string key, out T value) where T : class
        {
            return decoratedContext.Get(key, out value);
        }
    }
}
