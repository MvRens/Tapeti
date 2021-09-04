using System;
using CommandLine;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Tapeti.Cmd.Verbs
{
    [Verb("removequeue", HelpText = "Removes a durable queue.")]
    [ExecutableVerb(typeof(RemoveQueueVerb))]
    public class RemoveQueueOptions : BaseConnectionOptions
    {
        [Option('q', "queue", Required = true, HelpText = "The name of the queue to remove.")]
        public string QueueName { get; set; }

        [Option("confirm", HelpText = "Confirms the removal of the specified queue. If not provided, an interactive prompt will ask for confirmation.", Default = false)]
        public bool Confirm { get; set; }

        [Option("confirmpurge", HelpText = "Confirms the removal of the specified queue even if there still are messages in the queue. If not provided, an interactive prompt will ask for confirmation.", Default = false)]
        public bool ConfirmPurge { get; set; }
    }


    public class RemoveQueueVerb : IVerbExecuter
    {
        private readonly RemoveQueueOptions options;


        public RemoveQueueVerb(RemoveQueueOptions options)
        {
            this.options = options;
        }


        public void Execute()
        {
            if (!options.Confirm)
            {
                Console.Write($"Do you want to remove the queue '{options.QueueName}'? (Y/N) ");
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

            uint messageCount;

            try
            {
                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                messageCount = channel.QueueDelete(options.QueueName, true, true);
            }
            catch (OperationInterruptedException e)
            {
                if (e.ShutdownReason.ReplyCode == 406)
                {
                    if (!options.ConfirmPurge)
                    {
                        Console.Write($"There are messages remaining. Do you want to purge the queue '{options.QueueName}'? (Y/N) ");
                        var answer = Console.ReadLine();

                        if (string.IsNullOrEmpty(answer) || !answer.Equals("Y", StringComparison.CurrentCultureIgnoreCase))
                            return;
                    }

                    using var connection = factory.CreateConnection();
                    using var channel = connection.CreateModel();

                    messageCount = channel.QueueDelete(options.QueueName, true, false);
                }
                else
                    throw;
            }

            Console.WriteLine(messageCount == 0
                ? $"Empty or non-existent queue '{options.QueueName}' removed."
                : $"{messageCount} message{(messageCount != 1 ? "s" : "")} purged while removing '{options.QueueName}'.");
        }
    }
}
