using System;
using CommandLine;
using RabbitMQ.Client;

namespace Tapeti.Cmd.Verbs
{
    [Verb("purge", HelpText = "Removes all messages from a queue destructively.")]
    [ExecutableVerb(typeof(PurgeVerb))]
    public class PurgeOptions : BaseConnectionOptions
    {
        [Option('q', "queue", Required = true, HelpText = "The queue to purge.")]
        public string QueueName { get; set; }

        [Option("confirm", HelpText = "Confirms the purging of the specified queue. If not provided, an interactive prompt will ask for confirmation.", Default = false)]
        public bool Confirm { get; set; }
    }


    public class PurgeVerb : IVerbExecuter
    {
        private readonly PurgeOptions options;

        
        public PurgeVerb(PurgeOptions options)
        {
            this.options = options;
        }
        
        
        public void Execute()
        {
            if (!options.Confirm)
            {
                Console.Write($"Do you want to purge the queue '{options.QueueName}'? (Y/N) ");
                var answer = Console.ReadLine();

                if (string.IsNullOrEmpty(answer) || !answer.Equals("Y", StringComparison.CurrentCultureIgnoreCase))
                    return;
            }

            var factory = new ConnectionFactory
            {
                HostName = options.Host,
                Port = options.Port,
                VirtualHost = options.VirtualHost,
                UserName = options.Username,
                Password = options.Password
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            
            var messageCount = channel.QueuePurge(options.QueueName);

            Console.WriteLine($"{messageCount} message{(messageCount != 1 ? "s" : "")} purged from '{options.QueueName}'.");
        }
    }
}
