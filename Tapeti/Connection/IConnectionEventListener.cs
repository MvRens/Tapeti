namespace Tapeti.Connection
{
    public class DisconnectedEventArgs
    {
        public ushort ReplyCode;
        public string ReplyText;
    }


    public interface IConnectionEventListener
    {
        void Connected();
        void Reconnected();
        void Disconnected(DisconnectedEventArgs e);
    }
}
