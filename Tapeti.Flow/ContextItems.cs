namespace Tapeti.Flow
{
    /// <summary>
    /// Key names as used in the message context store. For internal use.
    /// </summary>
    public static class ContextItems
    {
        /// <summary>
        /// Key given to the FlowContext object as stored in the message context.
        /// </summary>
        public const string FlowContext = "Tapeti.Flow.FlowContext";

        /// <summary>
        /// Indicates if the current message handler is the last one to be called before a
        /// parallel flow is done and the convergeMethod will be called.
        /// Temporarily disables storing the flow state.
        /// </summary>
        public const string FlowIsConverging = "Tapeti.Flow.IsConverging";
    }
}
