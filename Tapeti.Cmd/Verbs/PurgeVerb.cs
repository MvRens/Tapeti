using CommandLine;
using RabbitMQ.Client;
using Tapeti.Cmd.ConsoleHelper;

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
        
        
        public void Execute(IConsole console)
        {
            var consoleWriter = console.GetPermanentWriter();
            
            if (!options.Confirm)
            {
                if (!consoleWriter.ConfirmYesNo($"Do you want to purge the queue '{options.QueueName}'?"))
                    return;
            }

            var factory = options.CreateConnectionFactory(console); using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            
            var messageCount = channel.QueuePurge(options.QueueName);

            consoleWriter.WriteLine($"{messageCount} message{(messageCount != 1 ? "s" : "")} purged from '{options.QueueName}'.");
        }
    }
}
