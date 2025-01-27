namespace Tapeti
{
    /// <summary>
    /// Tracks the number of currently running message handlers.
    /// </summary>
    public interface IMessageHandlerTracker
    {
        /// <summary>
        /// Registers the start of a message handler.
        /// </summary>
        void Enter();

        /// <summary>
        /// Registers the end of a message handler.
        /// </summary>
        void Exit();
    }
}
