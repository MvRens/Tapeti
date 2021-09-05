using System;
using System.Reflection;
using Tapeti.Config;

namespace Tapeti.Flow
{
    /// <inheritdoc />
    /// <summary>
    /// Provides information about the handler for the flow.
    /// </summary>
    public interface IFlowHandlerContext : IDisposable
    {
        /// <summary>
        /// Provides access to the Tapeti config.
        /// </summary>
        ITapetiConfig Config { get; }


        /// <summary>
        /// An instance of the controller which starts or continues the flow.
        /// </summary>
        object Controller { get; }


        /// <summary>
        /// Information about the method which starts or continues the flow.
        /// </summary>
        MethodInfo Method { get; }


        /// <summary>
        /// Access to the message context if this is a continuated flow.
        /// Will be null when in a starting flow.
        /// </summary>
        IMessageContext MessageContext { get; }
    }
}
