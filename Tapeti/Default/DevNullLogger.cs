using System;
using JetBrains.Annotations;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <summary>
    /// Default ILogger implementation which does not log anything.
    /// </summary>
    [PublicAPI]
    public class DevNullLogger : ILogger
    {
        /// <inheritdoc />
        public void Connect(ConnectContext context)
        {
        }

        /// <inheritdoc />
        public void ConnectFailed(ConnectFailedContext context)
        {
        }

        /// <inheritdoc />
        public void ConnectSuccess(ConnectSuccessContext context)
        {
        }

        /// <inheritdoc />
        public void Disconnect(DisconnectContext context)
        {
        }

        /// <inheritdoc />
        public void ConsumeStarted(ConsumeStartedContext context)
        {
        }

        /// <inheritdoc />
        public void ConsumeException(Exception exception, IMessageContext messageContext, ConsumeResult consumeResult)
        {
        }
    }
}
