namespace Tapeti.Connection
{
    public interface IConnectionEventListener
    {
        void Connected();
        void Reconnected();
        void Disconnected();
    }
}
