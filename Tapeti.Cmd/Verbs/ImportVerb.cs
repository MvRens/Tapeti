using System;
using System.IO;
using System.Net;
using System.Text;
using CommandLine;
using RabbitMQ.Client;
using Tapeti.Cmd.ConsoleHelper;
using Tapeti.Cmd.RateLimiter;
using Tapeti.Cmd.Serialization;

namespace Tapeti.Cmd.Verbs
{
    [Verb("import", HelpText = "Read messages from disk as previously exported and publish them to a queue.")]
    [ExecutableVerb(typeof(ImportVerb))]
    public class ImportOptions : BaseMessageSerializerOptions
    {
        [Option('i', "input", Group = "Input", HelpText = "Path or filename (depending on the chosen serialization method) where the messages will be read from.")]
        public string InputFile { get; set; }

        [Option('m', "message", Group = "Input", HelpText = "Single message to be sent, in the same format as used for SingleFileJSON. Serialization argument has no effect when using this input.")]
        public string InputMessage { get; set; }
        
        [Option('c', "pipe", Group = "Input", HelpText = "Messages are read from the standard input pipe, in the same format as used for SingleFileJSON. Serialization argument has no effect when using this input.")]
        public bool InputPipe { get; set; }
        
        [Option("urlencoded", HelpText = "Indicates the message is URL encoded. Only applies to messages passed directly with --message as quotes are very quirky on the command-line, even more so in PowerShell.")]
        public bool UrlEncoded { get; set; }

        [Option('e', "exchange", HelpText = "If specified publishes to the originating exchange using the original routing key. By default these are ignored and the message is published directly to the originating queue.")]
        public bool PublishToExchange { get; set; }
        
        [Option("skip", HelpText = "(Default: 0) Number of messages in the input to skip. Useful if a previous import was interrupted.", Default = 0)]
        public int Skip { get; set; }

        [Option('n', "maxcount", HelpText = "(Default: all) Maximum number of messages to import.")]
        public int? MaxCount { get; set; }

        [Option("maxrate", HelpText = "The maximum amount of messages per second to import.")]
        public int? MaxRate { get; set; }
        
        [Option("batchsize", HelpText = "How many messages to import before pausing. Will wait for manual confirmation unless batchpausetime is specified.")]
        public int? BatchSize { get; set; }
        
        [Option("batchpausetime", HelpText = "How many seconds to wait before starting the next batch if batchsize is specified.")]
        public int? BatchPauseTime { get; set; }
    }

    
    public class ImportVerb : IVerbExecuter
    {
        private readonly ImportOptions options;

        
        public ImportVerb(ImportOptions options)
        {
            this.options = options;
        }
        
        
        public void Execute(IConsole console)
        {
            var consoleWriter = console.GetPermanentWriter();
            var factory = options.CreateConnectionFactory(console); using var messageSerializer = GetMessageSerializer(options);
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            var rateLimiter = RateLimiterFactory.Create(console, options.MaxRate, options.BatchSize, options.BatchPauseTime);

            var totalCount = messageSerializer.GetMessageCount();
            var messageCount = 0;
            var skip = Math.Max(options.Skip, 0);


            ProgressBar progress = null;
            if (totalCount > 0)
                progress = new ProgressBar(console, totalCount);
            try
            {
                foreach (var message in messageSerializer.Deserialize(channel))
                {
                    if (console.Cancelled || (options.MaxCount.HasValue && messageCount >= options.MaxCount.Value))
                        break;

                    if (skip > 0)
                        skip--;
                    else
                        rateLimiter.Execute(() =>
                        {
                            if (console.Cancelled)
                                return;

                            var exchange = options.PublishToExchange ? message.Exchange : "";
                            var routingKey = options.PublishToExchange ? message.RoutingKey : message.Queue;

                            // ReSharper disable AccessToDisposedClosure
                            channel.BasicPublish(exchange, routingKey, message.Properties, message.Body);
                            messageCount++;

                            progress?.Report(messageCount);
                            // ReSharper restore AccessToDisposedClosure
                        });
                }
            }
            finally
            {
                progress?.Dispose();
            }

            consoleWriter.WriteLine($"{messageCount} message{(messageCount != 1 ? "s" : "")} published.");
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
                var inputMessage = options.UrlEncoded
                    ? WebUtility.UrlDecode(options.InputMessage)
                    : options.InputMessage;
                
                disposeStream = true;
                return new MemoryStream(Encoding.UTF8.GetBytes(inputMessage));
            }

            disposeStream = true;
            return new FileStream(options.InputFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
    }
}
