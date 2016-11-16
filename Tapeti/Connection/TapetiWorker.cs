using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Tapeti.Connection
{
    public class TapetiWorker
    {
        public string HostName { get; set; }
        public int Port { get; set; }
        public string VirtualHost { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }


        private IConnection connection;
        private IModel channel;


        public Task Close()
        {
            if (channel != null)
            {
                channel.Dispose();
                channel = null;
            }

            if (connection != null)
            {
                connection.Dispose();
                connection = null;
            }

            return Task.CompletedTask;
        }


        public IModel GetChannel()
        {
            if (channel != null)
                return channel;

            var connectionFactory = new ConnectionFactory
            {
                HostName = HostName,
                Port = Port,
                VirtualHost = VirtualHost,
                UserName = Username,
                Password = Password,
                AutomaticRecoveryEnabled = true
            };

            connection = connectionFactory.CreateConnection();
            channel = connection.CreateModel();

            return channel;
        }
    }
}
