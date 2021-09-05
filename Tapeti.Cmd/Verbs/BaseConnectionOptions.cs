using CommandLine;

namespace Tapeti.Cmd.Verbs
{
    public class BaseConnectionOptions
    {
        [Option('h', "host", HelpText = "Hostname of the RabbitMQ server.", Default = "localhost")]
        public string Host { get; set; }

        [Option("port", HelpText = "AMQP port of the RabbitMQ server.", Default = 5672)]
        public int Port { get; set; }

        [Option('v', "virtualhost", HelpText = "Virtual host used for the RabbitMQ connection.", Default = "/")]
        public string VirtualHost { get; set; }

        [Option('u', "username", HelpText = "Username used to connect to the RabbitMQ server.", Default = "guest")]
        public string Username { get; set; }

        [Option('p', "password", HelpText = "Password used to connect to the RabbitMQ server.", Default = "guest")]
        public string Password { get; set; }
    }
}
