using System;
using System.IO;
using System.Text;
using CommandLine;
using Tapeti.Cmd.ConsoleHelper;
using Tapeti.Cmd.Serialization;

namespace Tapeti.Cmd.Verbs
{
    [Verb("export", HelpText = "Fetch messages from a queue and write it to disk.")]
    [ExecutableVerb(typeof(ExportVerb))]
    public class ExportOptions : BaseMessageSerializerOptions
    {
        [Option('q', "queue", Required = true, HelpText = "The queue to read the messages from.")]
        public string QueueName { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path or filename (depending on the chosen serialization method) where the messages will be output to.")]
        public string OutputPath { get; set; }
        
        [Option('y', "overwrite", HelpText = "If the output exists, do not ask to overwrite.")]
        public bool Overwrite { get; set; }

        [Option('r', "remove", HelpText = "If specified messages are acknowledged and removed from the queue. If not messages are kept.")]
        public bool RemoveMessages { get; set; }

        [Option("skip", HelpText = "(Default: 0) Number of messages in the queue to skip. Useful if a previous non-removing export was interrupted.", Default = 0)]
        public int Skip { get; set; }

        [Option('n', "maxcount", HelpText = "(Default: all) Maximum number of messages to retrieve from the queue.")]
        public int? MaxCount { get; set; }
    }
    

    public class ExportVerb : IVerbExecuter
    {
        private readonly ExportOptions options;

        
        public ExportVerb(ExportOptions options)
        {
            this.options = options;
        }
        
        
        public void Execute(IConsole console)
        {
            var consoleWriter = console.GetPermanentWriter();
            
            using var messageSerializer = GetMessageSerializer(options, consoleWriter);
            if (messageSerializer == null)
                return;

            var factory = options.CreateConnectionFactory(console);
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            var totalCount = (int)channel.MessageCount(options.QueueName);

            var skip = Math.Max(options.Skip, 0);
            if (skip > 0)
                totalCount -= Math.Min(skip, totalCount);
            
            if (options.MaxCount.HasValue && options.MaxCount.Value < totalCount)
                totalCount = options.MaxCount.Value;

            consoleWriter.WriteLine($"Exporting {totalCount} message{(totalCount != 1 ? "s" : "")} (actual number may differ if queue has active consumers or publishers)");
            var messageCount = 0;
            
            using (var progressBar = new ProgressBar(console, totalCount))
            {
                while (!console.Cancelled && (!options.MaxCount.HasValue || messageCount < options.MaxCount.Value))
                {
                    var result = channel.BasicGet(options.QueueName, false);
                    if (result == null)
                        // No more messages on the queue
                        break;

                    if (skip > 0)
                        skip--;
                    else
                    {
                        messageCount++;

                        messageSerializer.Serialize(new Message
                        {
                            DeliveryTag = result.DeliveryTag,
                            Redelivered = result.Redelivered,
                            Exchange = result.Exchange,
                            RoutingKey = result.RoutingKey,
                            Queue = options.QueueName,
                            Properties = result.BasicProperties,
                            Body = result.Body.ToArray()
                        });

                        if (options.RemoveMessages)
                            channel.BasicAck(result.DeliveryTag, false);

                        progressBar.Report(messageCount);
                    }
                }
            }

            consoleWriter.WriteLine($"{messageCount} message{(messageCount != 1 ? "s" : "")} exported.");
        }


        private static IMessageSerializer GetMessageSerializer(ExportOptions options, IConsoleWriter consoleWriter)
        {
            switch (options.SerializationMethod)
            {
                case SerializationMethod.SingleFileJSON:
                    // ReSharper disable once InvertIf - causes two lines of "new SingleFileJSONMessageSerializer". DRY ReSharper.
                    if (!options.Overwrite && File.Exists(options.OutputPath))
                    {
                        if (!consoleWriter.ConfirmYesNo($"The output file '{options.OutputPath}' already exists, do you want to overwrite it?"))
                            return null;
                    }
                    
                    return new SingleFileJSONMessageSerializer(new FileStream(options.OutputPath, FileMode.Create, FileAccess.Write, FileShare.Read), true, Encoding.UTF8);

                case SerializationMethod.EasyNetQHosepipe:
                    // ReSharper disable once InvertIf - causes two lines of "new SingleFileJSONMessageSerializer". DRY ReSharper.
                    if (!options.Overwrite && EasyNetQMessageSerializer.OutputExists(options.OutputPath))
                    {
                        if (!consoleWriter.ConfirmYesNo($"The output path '{options.OutputPath}' already contains a previous export, do you want to overwrite it?"))
                            return null;
                    }

                    return new EasyNetQMessageSerializer(options.OutputPath);

                default:
                    throw new ArgumentOutOfRangeException(nameof(options.SerializationMethod), options.SerializationMethod, "Invalid SerializationMethod");
            }
        }
    }
}
