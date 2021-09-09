using System;
using System.Collections.Generic;
using CommandLine;
using RabbitMQ.Client;
using Tapeti.Cmd.ConsoleHelper;
using Tapeti.Cmd.Parser;

namespace Tapeti.Cmd.Verbs
{
    [Verb("declarequeue", HelpText = "Declares a durable queue without arguments.")]
    [ExecutableVerb(typeof(DeclareQueueVerb))]
    public class DeclareQueueOptions : BaseConnectionOptions
    {
        [Option('q', "queue", Required = true, HelpText = "The name of the queue to declare.")]
        public string QueueName { get; set; }

        [Option('b', "bindings", Required = false, HelpText = "One or more bindings to add to the queue. Format: <exchange>:<routingKey>")]
        public IEnumerable<string> Bindings { get; set; }
    }

    
    public class DeclareQueueVerb : IVerbExecuter
    {
        private readonly DeclareQueueOptions options;

        
        public DeclareQueueVerb(DeclareQueueOptions options)
        {
            this.options = options;
        }
        
        
        public void Execute(IConsole console)
        {
            var consoleWriter = console.GetPermanentWriter();
            
            // Parse early to fail early
            var bindings = BindingParser.Parse(options.Bindings);

            var factory = options.CreateConnectionFactory(console);
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(options.QueueName, true, false, false);

            foreach (var (exchange, routingKey) in bindings)
                channel.QueueBind(options.QueueName, exchange, routingKey);

            consoleWriter.WriteLine($"Queue {options.QueueName} declared with {bindings.Length} binding{(bindings.Length != 1 ? "s" : "")}.");
        }
    }
}
