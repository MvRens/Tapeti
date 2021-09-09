using System;
using System.Collections.Generic;
using CommandLine;
using RabbitMQ.Client;
using Tapeti.Cmd.ConsoleHelper;
using Tapeti.Cmd.Parser;

namespace Tapeti.Cmd.Verbs
{
    [Verb("bindqueue", HelpText = "Add a binding to an existing queue.")]
    [ExecutableVerb(typeof(BindQueueVerb))]
    public class BindQueueOptions : BaseConnectionOptions
    {
        [Option('q', "queue", Required = true, HelpText = "The name of the queue to add the binding(s) to.")]
        public string QueueName { get; set; }

        [Option('b', "bindings", Required = false, HelpText = "One or more bindings to add to the queue. Format: <exchange>:<routingKey>")]
        public IEnumerable<string> Bindings { get; set; }
    }
    
    
    public class BindQueueVerb : IVerbExecuter
    {
        private readonly BindQueueOptions options;


        public BindQueueVerb(BindQueueOptions options)
        {
            this.options = options;
        }


        public void Execute(IConsole console)
        {
            var consoleWriter = console.GetPermanentWriter();
            var bindings = BindingParser.Parse(options.Bindings);

            var factory = options.CreateConnectionFactory(console);
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            foreach (var (exchange, routingKey) in bindings)
                channel.QueueBind(options.QueueName, exchange, routingKey);
            
            consoleWriter.WriteLine($"{bindings.Length} binding{(bindings.Length != 1 ? "s" : "")} added to queue {options.QueueName}.");
        }
    }
}
