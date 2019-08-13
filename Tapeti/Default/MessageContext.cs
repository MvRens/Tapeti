using Tapeti.Config;

namespace Tapeti.Default
{
    /// <inheritdoc />
    public class MessageContext : IMessageContext
    {
        /// <inheritdoc />
        public ITapetiConfig Config { get; set; }

        /// <inheritdoc />
        public string Queue { get; set; }

        /// <inheritdoc />
        public string Exchange { get; set; }

        /// <inheritdoc />
        public string RoutingKey { get; set; }

        /// <inheritdoc />
        public object Message { get; set; }

        /// <inheritdoc />
        public IMessageProperties Properties { get; set; }

        /// <inheritdoc />
        public IBinding Binding { get; set; }

        /// <inheritdoc />
        public virtual void Dispose()
        {
        }
    }
}
