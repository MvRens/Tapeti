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
        public void Connect(TapetiConnectionParams connectionParams, bool isReconnect)
        {
        }

        /// <inheritdoc />
        public void ConnectFailed(TapetiConnectionParams connectionParams, Exception exception)
        {
        }

        /// <inheritdoc />
        public void ConnectSuccess(TapetiConnectionParams connectionParams, bool isReconnect)
        {
        }

        /// <inheritdoc />
        public void ConsumeException(Exception exception, IMessageContext messageContext, ConsumeResult consumeResult)
        {
        }
    }
}
