using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using CommandLine;
using RabbitMQ.Client;
using Tapeti.Cmd.ConsoleHelper;
using Tapeti.Cmd.Mock;
using Tapeti.Cmd.Serialization;

namespace Tapeti.Cmd.Verbs
{
    [Verb("example", HelpText = "Output an example SingleFileJSON formatted message.")]
    [ExecutableVerb(typeof(ExampleVerb))]
    public class ExampleOptions
    {
    }


    public class ExampleVerb : IVerbExecuter
    {
        public ExampleVerb(ExampleOptions options)
        {
            // Prevent compiler warnings, the parameter is expected by the Activator
            Debug.Assert(options != null);
        }


        public void Execute(IConsole console)
        {
            using var messageSerializer = new SingleFileJSONMessageSerializer(Console.OpenStandardOutput(), false, new UTF8Encoding(false));
            
            messageSerializer.Serialize(new Message
            {
                Exchange = "example",
                Queue = "example.queue",
                RoutingKey = "example.routing.key",
                DeliveryTag = 42,
                Properties = new MockBasicProperties
                {
                    ContentType = "application/json",
                    DeliveryMode = 2,
                    Headers = new Dictionary<string, object>
                    {
                        { "classType", Encoding.UTF8.GetBytes("Tapeti.Cmd.Example:Tapeti.Cmd") }
                    },
                    ReplyTo = "reply.queue",
                    Timestamp = new AmqpTimestamp(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds())
                },
                Body = Encoding.UTF8.GetBytes("{ \"Hello\": \"world!\" }")
            });
        }
    }
}
