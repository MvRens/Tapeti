namespace Tapeti.Connection
{
    /// <summary>
    /// Receives notifications on the state of the connection.
    /// </summary>
    public interface IConnectionEventListener
    {
        /// <summary>
        /// Called when a connection to RabbitMQ has been established.
        /// </summary>
        void Connected();


        /// <summary>
        /// Called when the connection to RabbitMQ has been lost.
        /// </summary>
        void Reconnected();


        /// <summary>
        /// Called when the connection to RabbitMQ has been recovered after an unexpected disconnect.
        /// </summary>
        void Disconnected(DisconnectedEventArgs e);
    }
}
