using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Tapeti.Cmd.Commands;
using Tapeti.Cmd.Mock;
using Tapeti.Cmd.RateLimiter;
using Tapeti.Cmd.Serialization;

namespace Tapeti.Cmd
{
    public class Program
    {
        public class CommonOptions
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


        public enum SerializationMethod
        {
            SingleFileJSON,
            EasyNetQHosepipe
        }


        public class MessageSerializerOptions : CommonOptions
        {
            [Option('s', "serialization", HelpText = "The method used to serialize the message for import or export. Valid options: SingleFileJSON, EasyNetQHosepipe.", Default = SerializationMethod.SingleFileJSON)]
            public SerializationMethod SerializationMethod { get; set; }
        }



        [Verb("export", HelpText = "Fetch messages from a queue and write it to disk.")]
        public class ExportOptions : MessageSerializerOptions
        {
            [Option('q', "queue", Required = true, HelpText = "The queue to read the messages from.")]
            public string QueueName { get; set; }

            [Option('o', "output", Required = true, HelpText = "Path or filename (depending on the chosen serialization method) where the messages will be output to.")]
            public string OutputPath { get; set; }

            [Option('r', "remove", HelpText = "If specified messages are acknowledged and removed from the queue. If not messages are kept.")]
            public bool RemoveMessages { get; set; }

            [Option('n', "maxcount", HelpText = "(Default: all) Maximum number of messages to retrieve from the queue.")]
            public int? MaxCount { get; set; }
        }


        [Verb("import", HelpText = "Read messages from disk as previously exported and publish them to a queue.")]
        public class ImportOptions : MessageSerializerOptions
        {
            [Option('i', "input", Group = "Input", HelpText = "Path or filename (depending on the chosen serialization method) where the messages will be read from.")]
            public string InputFile { get; set; }

            [Option('m', "message", Group = "Input", HelpText = "Single message to be sent, in the same format as used for SingleFileJSON. Serialization argument has no effect when using this input.")]
            public string InputMessage { get; set; }

            [Option('c', "pipe", Group = "Input", HelpText = "Messages are read from the standard input pipe, in the same format as used for SingleFileJSON. Serialization argument has no effect when using this input.")]
            public bool InputPipe { get; set; }

            [Option('e', "exchange", HelpText = "If specified publishes to the originating exchange using the original routing key. By default these are ignored and the message is published directly to the originating queue.")]
            public bool PublishToExchange { get; set; }

            [Option("maxrate", HelpText = "The maximum amount of messages per second to import.")]
            public int? MaxRate { get; set; }
        }


        [Verb("example", HelpText = "Output an example SingleFileJSON formatted message.")]
        public class ExampleOptions
        {
        }


        [Verb("shovel", HelpText = "Reads messages from a queue and publishes them to another queue, optionally to another RabbitMQ server.")]
        public class ShovelOptions : CommonOptions
        {
            [Option('q', "queue", Required = true, HelpText = "The queue to read the messages from.")]
            public string QueueName { get; set; }

            [Option('t', "targetqueue", HelpText = "The target queue to publish the messages to. Defaults to the source queue if a different target host, port or virtualhost is specified. Otherwise it must be different from the source queue.")]
            public string TargetQueueName { get; set; }

            [Option('r', "remove", HelpText = "If specified messages are acknowledged and removed from the source queue. If not messages are kept.")]
            public bool RemoveMessages { get; set; }

            [Option('n', "maxcount", HelpText = "(Default: all) Maximum number of messages to retrieve from the queue.")]
            public int? MaxCount { get; set; }

            [Option("targethost", HelpText = "Hostname of the target RabbitMQ server. Defaults to the source host. Note that you may still specify a different targetusername for example.")]
            public string TargetHost { get; set; }

            [Option("targetport", HelpText = "AMQP port of the target RabbitMQ server. Defaults to the source port.")]
            public int? TargetPort { get; set; }

            [Option("targetvirtualhost", HelpText = "Virtual host used for the target RabbitMQ connection. Defaults to the source virtualhost.")]
            public string TargetVirtualHost { get; set; }

            [Option("targetusername", HelpText = "Username used to connect to the target RabbitMQ server. Defaults to the source username.")]
            public string TargetUsername { get; set; }

            [Option("targetpassword", HelpText = "Password used to connect to the target RabbitMQ server. Defaults to the source password.")]
            public string TargetPassword { get; set; }

            [Option("maxrate", HelpText = "The maximum amount of messages per second to shovel.")]
            public int? MaxRate { get; set; }
        }


        [Verb("purge", HelpText = "Removes all messages from a queue destructively.")]
        public class PurgeOptions : CommonOptions
        {
            [Option('q', "queue", Required = true, HelpText = "The queue to purge.")]
            public string QueueName { get; set; }

            [Option("confirm", HelpText = "Confirms the purging of the specified queue. If not provided, an interactive prompt will ask for confirmation.", Default = false)]
            public bool Confirm { get; set; }
        }


        [Verb("declarequeue", HelpText = "Declares a durable queue without arguments, compatible with Tapeti.")]
        public class DeclareQueueOptions : CommonOptions
        {
            [Option('q', "queue", Required = true, HelpText = "The name of the queue to declare.")]
            public string QueueName { get; set; }
            
            [Option('b', "bindings", Required = false, HelpText = "One or more bindings to add to the queue. Format: <exchange>:<routingKey>")]
            public IEnumerable<string> Bindings { get; set; }
        }


        [Verb("removequeue", HelpText = "Removes a durable queue.")]
        public class RemoveQueueOptions : CommonOptions
        {
            [Option('q', "queue", Required = true, HelpText = "The name of the queue to remove.")]
            public string QueueName { get; set; }
            
            [Option("confirm", HelpText = "Confirms the removal of the specified queue. If not provided, an interactive prompt will ask for confirmation.", Default = false)]
            public bool Confirm { get; set; }
            
            [Option("confirmpurge", HelpText = "Confirms the removal of the specified queue even if there still are messages in the queue. If not provided, an interactive prompt will ask for confirmation.", Default = false)]
            public bool ConfirmPurge { get; set; }
        }


        [Verb("bindqueue", HelpText = "Add a binding to a queue.")]
        public class BindQueueOptions : CommonOptions
        {
            [Option('q', "queue", Required = true, HelpText = "The name of the queue to add the binding(s) to.")]
            public string QueueName { get; set; }

            [Option('b', "bindings", Required = false, HelpText = "One or more bindings to add to the queue. Format: <exchange>:<routingKey>")]
            public IEnumerable<string> Bindings { get; set; }
        }


        [Verb("unbindqueue", HelpText = "Remove a binding from a queue.")]
        public class UnbindQueueOptions : CommonOptions
        {
            [Option('q', "queue", Required = true, HelpText = "The name of the queue to remove the binding(s) from.")]
            public string QueueName { get; set; }

            [Option('b', "bindings", Required = false, HelpText = "One or more bindings to remove from the queue. Format: <exchange>:<routingKey>")]
            public IEnumerable<string> Bindings { get; set; }
        }


        public static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<ExportOptions, ImportOptions, ShovelOptions, PurgeOptions, ExampleOptions, 
                    DeclareQueueOptions, RemoveQueueOptions, BindQueueOptions, UnbindQueueOptions>(args)
                .MapResult(
                    (ExportOptions o) => ExecuteVerb(o, RunExport),
                    (ImportOptions o) => ExecuteVerb(o, RunImport),
                    (ExampleOptions o) => ExecuteVerb(o, RunExample),
                    (ShovelOptions o) => ExecuteVerb(o, RunShovel),
                    (PurgeOptions o) => ExecuteVerb(o, RunPurge),
                    (DeclareQueueOptions o) => ExecuteVerb(o, RunDeclareQueue),
                    (RemoveQueueOptions o) => ExecuteVerb(o, RunRemoveQueue),
                    (BindQueueOptions o) => ExecuteVerb(o, RunBindQueue),
                    (UnbindQueueOptions o) => ExecuteVerb(o, RunUnbindQueue),
                    errs =>
                    {
                        if (!Debugger.IsAttached) 
                            return 1;

                        Console.WriteLine("Press any Enter key to continue...");
                        Console.ReadLine();
                        return 1;
                    }
                );
        }


        private static int ExecuteVerb<T>(T options, Action<T> execute) where T : class
        {
            try
            {
                execute(options);
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return 1;
            }
        }


        private static IConnection GetConnection(CommonOptions options)
        {
            var factory = new ConnectionFactory
            {
                HostName = options.Host,
                Port = options.Port,
                VirtualHost = options.VirtualHost,
                UserName = options.Username,
                Password = options.Password
            };

            return factory.CreateConnection();
        }


        private static IMessageSerializer GetMessageSerializer(ImportOptions options)
        {
            switch (options.SerializationMethod)
            {
                case SerializationMethod.SingleFileJSON:
                    return new SingleFileJSONMessageSerializer(GetInputStream(options, out var disposeStream), disposeStream, Encoding.UTF8);

                case SerializationMethod.EasyNetQHosepipe:
                    if (string.IsNullOrEmpty(options.InputFile))
                        throw new ArgumentException("An input path must be provided when using EasyNetQHosepipe serialization");

                    return new EasyNetQMessageSerializer(options.InputFile);

                default:
                    throw new ArgumentOutOfRangeException(nameof(options.SerializationMethod), options.SerializationMethod, "Invalid SerializationMethod");
            }
        }


        private static Stream GetInputStream(ImportOptions options, out bool disposeStream)
        {
            if (options.InputPipe)
            {
                disposeStream = false;
                return Console.OpenStandardInput();
            }

            if (!string.IsNullOrEmpty(options.InputMessage))
            {
                disposeStream = true;
                return new MemoryStream(Encoding.UTF8.GetBytes(options.InputMessage));
            }

            disposeStream = true;
            return new FileStream(options.InputFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }


        private static IMessageSerializer GetMessageSerializer(ExportOptions options)
        {
            switch (options.SerializationMethod)
            {
                case SerializationMethod.SingleFileJSON:
                    return new SingleFileJSONMessageSerializer(GetOutputStream(options, out var disposeStream), disposeStream, Encoding.UTF8);

                case SerializationMethod.EasyNetQHosepipe:
                    if (string.IsNullOrEmpty(options.OutputPath))
                        throw new ArgumentException("An output path must be provided when using EasyNetQHosepipe serialization");

                    return new EasyNetQMessageSerializer(options.OutputPath);

                default:
                    throw new ArgumentOutOfRangeException(nameof(options.SerializationMethod), options.SerializationMethod, "Invalid SerializationMethod");
            }
        }


        private static Stream GetOutputStream(ExportOptions options, out bool disposeStream)
        {
            disposeStream = true;
            return new FileStream(options.OutputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        }


        private static IRateLimiter GetRateLimiter(int? maxRate)
        {
            if (!maxRate.HasValue || maxRate.Value <= 0)
                return new NoRateLimiter();

            return new SpreadRateLimiter(maxRate.Value, TimeSpan.FromSeconds(1));
        }


        private static void RunExport(ExportOptions options)
        {
            int messageCount;

            using (var messageSerializer = GetMessageSerializer(options))
            using (var connection = GetConnection(options))
            using (var channel = connection.CreateModel())
            {
                messageCount = new ExportCommand
                {
                    MessageSerializer = messageSerializer,

                    QueueName = options.QueueName,
                    RemoveMessages = options.RemoveMessages,
                    MaxCount = options.MaxCount
                }.Execute(channel);
            }

            Console.WriteLine($"{messageCount} message{(messageCount != 1 ? "s" : "")} exported.");
        }


        private static void RunImport(ImportOptions options)
        {
            int messageCount;

            using (var messageSerializer = GetMessageSerializer(options))
            using (var connection = GetConnection(options))
            using (var channel = connection.CreateModel())
            {
                messageCount = new ImportCommand
                {
                    MessageSerializer = messageSerializer,

                    DirectToQueue = !options.PublishToExchange
                }.Execute(channel, GetRateLimiter(options.MaxRate));
            }

            Console.WriteLine($"{messageCount} message{(messageCount != 1 ? "s" : "")} published.");
        }


        private static void RunExample(ExampleOptions options)
        {
            using (var messageSerializer = new SingleFileJSONMessageSerializer(Console.OpenStandardOutput(), false, new UTF8Encoding(false)))
            {
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

        
        private static void RunShovel(ShovelOptions options)
        {
            int messageCount;

            using (var sourceConnection = GetConnection(options))
            using (var sourceChannel = sourceConnection.CreateModel())
            {
                var shovelCommand = new ShovelCommand
                {
                    QueueName = options.QueueName,
                    TargetQueueName = !string.IsNullOrEmpty(options.TargetQueueName) ? options.TargetQueueName : options.QueueName,
                    RemoveMessages = options.RemoveMessages,
                    MaxCount = options.MaxCount
                };


                if (RequiresSecondConnection(options))
                {
                    using (var targetConnection = GetTargetConnection(options))
                    using (var targetChannel = targetConnection.CreateModel())
                    {
                        messageCount = shovelCommand.Execute(sourceChannel, targetChannel, GetRateLimiter(options.MaxRate));
                    }
                }
                else
                    messageCount = shovelCommand.Execute(sourceChannel, sourceChannel, GetRateLimiter(options.MaxRate));
            }

            Console.WriteLine($"{messageCount} message{(messageCount != 1 ? "s" : "")} shoveled.");
        }


        private static bool RequiresSecondConnection(ShovelOptions options)
        {
            if (!string.IsNullOrEmpty(options.TargetHost) && options.TargetHost != options.Host)
                return true;

            if (options.TargetPort.HasValue && options.TargetPort.Value != options.Port)
                return true;

            if (!string.IsNullOrEmpty(options.TargetVirtualHost) && options.TargetVirtualHost != options.VirtualHost)
                return true;


            // All relevant target host parameters are either omitted or the same. This means the queue must be different
            // to prevent an infinite loop.
            if (string.IsNullOrEmpty(options.TargetQueueName) || options.TargetQueueName == options.QueueName)
                throw new ArgumentException("Target queue must be different from the source queue when shoveling within the same (virtual) host");


            if (!string.IsNullOrEmpty(options.TargetUsername) && options.TargetUsername != options.Username)
                return true;

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (!string.IsNullOrEmpty(options.TargetPassword) && options.TargetPassword != options.Password)
                return true;

            
            // Everything's the same, we can use the same channel
            return false;
        }


        private static IConnection GetTargetConnection(ShovelOptions options)
        {
            var factory = new ConnectionFactory
            {
                HostName = !string.IsNullOrEmpty(options.TargetHost) ? options.TargetHost : options.Host,
                Port = options.TargetPort ?? options.Port,
                VirtualHost = !string.IsNullOrEmpty(options.TargetVirtualHost) ? options.TargetVirtualHost : options.VirtualHost,
                UserName = !string.IsNullOrEmpty(options.TargetUsername) ? options.TargetUsername : options.Username,
                Password = !string.IsNullOrEmpty(options.TargetPassword) ? options.TargetPassword : options.Password
            };

            return factory.CreateConnection();
        }


        private static void RunPurge(PurgeOptions options)
        {
            if (!options.Confirm)
            {
                Console.Write($"Do you want to purge the queue '{options.QueueName}'? (Y/N) ");
                var answer = Console.ReadLine();

                if (string.IsNullOrEmpty(answer) || !answer.Equals("Y", StringComparison.CurrentCultureIgnoreCase))
                    return;
            }

            uint messageCount;

            using (var connection = GetConnection(options))
            using (var channel = connection.CreateModel())
            {
                messageCount = channel.QueuePurge(options.QueueName);
            }

            Console.WriteLine($"{messageCount} message{(messageCount != 1 ? "s" : "")} purged from '{options.QueueName}'.");
        }


        private static void RunDeclareQueue(DeclareQueueOptions options)
        {
            // Parse early to fail early
            var bindings = ParseBindings(options.Bindings);
            
            using (var connection = GetConnection(options))
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(options.QueueName, true, false, false);

                foreach (var (exchange, routingKey) in bindings)
                    channel.QueueBind(options.QueueName, exchange, routingKey);
            }

            Console.WriteLine($"Queue {options.QueueName} declared with {bindings.Length} binding{(bindings.Length != 1 ? "s" : "")}.");
        }


        private static void RunRemoveQueue(RemoveQueueOptions options)
        {
            if (!options.Confirm)
            {
                Console.Write($"Do you want to remove the queue '{options.QueueName}'? (Y/N) ");
                var answer = Console.ReadLine();

                if (string.IsNullOrEmpty(answer) || !answer.Equals("Y", StringComparison.CurrentCultureIgnoreCase))
                    return;
            }

            uint messageCount;

            try
            {
                using (var connection = GetConnection(options))
                using (var channel = connection.CreateModel())
                {
                    messageCount = channel.QueueDelete(options.QueueName, true, true);
                }
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

                    using (var connection = GetConnection(options))
                    using (var channel = connection.CreateModel())
                    {
                        messageCount = channel.QueueDelete(options.QueueName, true, false);
                    }
                }
                else
                    throw;
            }

            Console.WriteLine(messageCount == 0 
                ? $"Empty or non-existent queue '{options.QueueName}' removed." 
                : $"{messageCount} message{(messageCount != 1 ? "s" : "")} purged while removing '{options.QueueName}'.");
        }


        private static void RunBindQueue(BindQueueOptions options)
        {
            var bindings = ParseBindings(options.Bindings);

            using (var connection = GetConnection(options))
            using (var channel = connection.CreateModel())
            {
                foreach (var (exchange, routingKey) in bindings)
                    channel.QueueBind(options.QueueName, exchange, routingKey);
            }

            Console.WriteLine($"{bindings.Length} binding{(bindings.Length != 1 ? "s" : "")} added to queue {options.QueueName}.");
        }


        private static void RunUnbindQueue(UnbindQueueOptions options)
        {
            var bindings = ParseBindings(options.Bindings);

            using (var connection = GetConnection(options))
            using (var channel = connection.CreateModel())
            {
                foreach (var (exchange, routingKey) in bindings)
                    channel.QueueUnbind(options.QueueName, exchange, routingKey);
            }

            Console.WriteLine($"{bindings.Length} binding{(bindings.Length != 1 ? "s" : "")} removed from queue {options.QueueName}.");
        }



        private static Tuple<string, string>[] ParseBindings(IEnumerable<string> bindings)
        {
            return bindings
                .Select(b =>
                {
                    var parts = b.Split(':');
                    if (parts.Length != 2)
                        throw new InvalidOperationException($"Invalid binding format: {b}");

                    return new Tuple<string, string>(parts[0], parts[1]);
                })
                .ToArray();
        }
    }
}
