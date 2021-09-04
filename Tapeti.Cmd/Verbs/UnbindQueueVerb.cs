using System;
using System.Collections.Generic;
using CommandLine;
using RabbitMQ.Client;
using Tapeti.Cmd.Parser;

namespace Tapeti.Cmd.Verbs
{
    [Verb("unbindqueue", HelpText = "Remove a binding from a queue.")]
    [ExecutableVerb(typeof(UnbindQueueVerb))]
    public class UnbindQueueOptions : BaseConnectionOptions
    {
        [Option('q', "queue", Required = true, HelpText = "The name of the queue to remove the binding(s) from.")]
        public string QueueName { get; set; }

        [Option('b', "bindings", Required = false, HelpText = "One or more bindings to remove from the queue. Format: <exchange>:<routingKey>")]
        public IEnumerable<string> Bindings { get; set; }
    }


    public class UnbindQueueVerb : IVerbExecuter
    {
        private readonly UnbindQueueOptions options;

        
        public UnbindQueueVerb(UnbindQueueOptions options)
        {
            this.options = options;
        }
        
        
        public void Execute()
        {
            var bindings = BindingParser.Parse(options.Bindings);

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
            
            foreach (var (exchange, routingKey) in bindings)
                channel.QueueUnbind(options.QueueName, exchange, routingKey);

            Console.WriteLine($"{bindings.Length} binding{(bindings.Length != 1 ? "s" : "")} removed from queue {options.QueueName}.");
        }
    }
}
