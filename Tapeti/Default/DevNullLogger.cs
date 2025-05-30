using System;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <summary>
    /// Default ILogger implementation which does not log anything.
    /// </summary>
    public class DevNullLogger : ILogger
    {
        /// <inheritdoc />
        public void Connect(ConnectContext connectContext)
        {
        }

        /// <inheritdoc />
        public void ConnectFailed(ConnectFailedContext connectContext)
        {
        }

        /// <inheritdoc />
        public void ConnectSuccess(ConnectSuccessContext connectContext)
        {
        }

        /// <inheritdoc />
        public void Disconnect(DisconnectContext disconnectContext)
        {
        }

        /// <inheritdoc />
        public void ConsumeException(Exception exception, IMessageContext messageContext, ConsumeResult consumeResult)
        {
        }
    }
}
