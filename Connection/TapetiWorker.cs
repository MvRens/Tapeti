using System;
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
        private readonly Lazy<TaskQueue> taskQueue = new Lazy<TaskQueue>();


        public Task Publish(object message)
        {
            return taskQueue.Value.Add(() =>
            {
                //GetChannel().BasicPublish();
            });
        }


        public void ApplyTopology(IMessageHandlerRegistration registration)
        {
            registration.ApplyTopology(GetChannel());
        }


        public Task Close()
        {
            if (channel != null)
            {
                channel.Dispose();
                channel = null;
            }

            // ReSharper disable once InvertIf
            if (connection != null)
            {
                connection.Dispose();
                connection = null;
            }

            return Task.CompletedTask;
        }


        private IModel GetChannel()
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


        private class ScheduledWorkItem
        {
            
        }
    }
}
