using System;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <inheritdoc />
    /// <summary>
    /// Default ILogger implementation which does not log anything.
    /// </summary>
    public class DevNullLogger : ILogger
    {
        /// <inheritdoc />
        public void Connect(IConnectContext connectContext)
        {
        }

        /// <inheritdoc />
        public void ConnectFailed(IConnectFailedContext connectContext)
        {
        }

        /// <inheritdoc />
        public void ConnectSuccess(IConnectSuccessContext connectContext)
        {
        }

        /// <inheritdoc />
        public void Disconnect(IDisconnectContext disconnectContext)
        {
        }

        /// <inheritdoc />
        public void ConsumeException(Exception exception, IMessageContext messageContext, ConsumeResult consumeResult)
        {
        }

        /// <inheritdoc />
        public void QueueObsolete(string queueName, bool deleted, uint messageCount)
        {
        }
    }
}
