using System;
using System.Diagnostics;
using CommandLine;
using RabbitMQ.Client;
using Tapeti.Cmd.Commands;
using Tapeti.Cmd.Serialization;

namespace Tapeti.Cmd
{
    public class Program
    {
        public class CommonOptions
        {
            [Option('h', "host", HelpText = "Hostname of the RabbitMQ server.", Default = "localhost")]
            public string Host { get; set; }

            [Option('p', "port", HelpText = "AMQP port of the RabbitMQ server.", Default = 5672)]
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
            [Option('i', "input", Required = true, HelpText = "Path or filename (depending on the chosen serialization method) where the messages will be read from.")]
            public string Input { get; set; }

            [Option('e', "exchange", HelpText = "If specified publishes to the originating exchange using the original routing key. By default these are ignored and the message is published directly to the originating queue.")]
            public bool PublishToExchange { get; set; }
        }



        public static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<ExportOptions, ImportOptions>(args)
                .MapResult(
                    (ExportOptions o) => ExecuteVerb(o, RunExport),
                    (ImportOptions o) => ExecuteVerb(o, RunImport),
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


        private static ConnectionFactory GetConnectionFactory(CommonOptions options)
        {
            return new ConnectionFactory
            {
                HostName = options.Host,
                Port = options.Port,
                VirtualHost = options.VirtualHost,
                UserName = options.Username,
                Password = options.Password
            };
        }


        private static IMessageSerializer GetMessageSerializer(MessageSerializerOptions options, string path)
        {
            switch (options.SerializationMethod)
            {
                case SerializationMethod.SingleFileJSON:
                    return new SingleFileJSONMessageSerializer(path);

                case SerializationMethod.EasyNetQHosepipe:
                    throw new NotImplementedException();

                default:
                    throw new ArgumentOutOfRangeException(nameof(options.SerializationMethod), options.SerializationMethod, "Invalid SerializationMethod");
            }
        }


        private static void RunExport(ExportOptions options)
        {
            int messageCount;

            using (var messageSerializer = GetMessageSerializer(options, options.OutputPath))
            {
                messageCount = new ExportCommand
                {
                    ConnectionFactory = GetConnectionFactory(options),
                    MessageSerializer = messageSerializer,

                    QueueName = options.QueueName,
                    RemoveMessages = options.RemoveMessages,
                    MaxCount = options.MaxCount
                }.Execute();
            }

            Console.WriteLine($"{messageCount} message{(messageCount != 1 ? "s" : "")} exported.");
        }


        private static void RunImport(ImportOptions options)
        {
            int messageCount;

            using (var messageSerializer = GetMessageSerializer(options, options.Input))
            {
                messageCount = new ImportCommand
                {
                    ConnectionFactory = GetConnectionFactory(options),
                    MessageSerializer = messageSerializer,

                    DirectToQueue = !options.PublishToExchange
                }.Execute();
            }

            Console.WriteLine($"{messageCount} message{(messageCount != 1 ? "s" : "")} published.");
        }
    }
}
