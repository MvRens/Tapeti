using System;

namespace Tapeti.Annotations
{
    /// <inheritdoc />
    /// <summary>
    /// Can be attached to a message class to specify that the receiver of the message must
    /// provide a response message of the type specified in the Response attribute. This response
    /// must be sent by either returning it from the message handler method or using
    /// EndWithResponse when using Tapeti Flow. These methods will respond directly
    /// to the queue specified in the reply-to header automatically added by Tapeti.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class RequestAttribute : Attribute
    {
        /// <summary>
        /// The type of the message class which must be returned as the response.
        /// </summary>
        public Type Response { get; set; }
    }
}
