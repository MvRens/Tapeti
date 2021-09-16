using System;
using CommandLine;
using RabbitMQ.Client;
using Tapeti.Cmd.ConsoleHelper;
using Tapeti.Cmd.RateLimiter;

namespace Tapeti.Cmd.Verbs
{
    [Verb("shovel", HelpText = "Reads messages from a queue and publishes them to another queue, optionally to another RabbitMQ server.")]
    [ExecutableVerb(typeof(ShovelVerb))]
    public class ShovelOptions : BaseConnectionOptions
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

        [Option("skip", HelpText = "(Default: 0) Number of messages in the queue to skip. Useful if a previous non-removing shovel was interrupted.", Default = 0)]
        public int Skip { get; set; }

        [Option("maxrate", HelpText = "The maximum amount of messages per second to shovel.")]
        public int? MaxRate { get; set; }

        [Option("batchsize", HelpText = "How many messages to shovel before pausing. Will wait for manual confirmation unless batchpausetime is specified.")]
        public int? BatchSize { get; set; }

        [Option("batchpausetime", HelpText = "How many seconds to wait before starting the next batch if batchsize is specified.")]
        public int? BatchPauseTime { get; set; }
    }
    
    
    public class ShovelVerb : IVerbExecuter
    {
        private readonly ShovelOptions options;

        
        public ShovelVerb(ShovelOptions options)
        {
            this.options = options;
        }
        
        
        public void Execute(IConsole console)
        {
            var sourceFactory = options.CreateConnectionFactory(console);
            using var sourceConnection = sourceFactory.CreateConnection();
            using var sourceChannel = sourceConnection.CreateModel();

            if (RequiresSecondConnection(options))
            {
                var targetFactory = new ConnectionFactory
                {
                    HostName = !string.IsNullOrEmpty(options.TargetHost) ? options.TargetHost : options.Host,
                    Port = options.TargetPort ?? options.Port ?? 5672,
                    VirtualHost = !string.IsNullOrEmpty(options.TargetVirtualHost) ? options.TargetVirtualHost : options.VirtualHost,
                    UserName = !string.IsNullOrEmpty(options.TargetUsername) ? options.TargetUsername : options.Username,
                    Password = !string.IsNullOrEmpty(options.TargetPassword) ? options.TargetPassword : options.Password
                };

                using var targetConnection = targetFactory.CreateConnection();
                using var targetChannel = targetConnection.CreateModel();
                
                Shovel(console, options, sourceChannel, targetChannel);
            }
            else
                Shovel(console, options, sourceChannel, sourceChannel);
        }
        
        
        private static void Shovel(IConsole console, ShovelOptions options, IModel sourceChannel, IModel targetChannel)
        {
            var consoleWriter = console.GetPermanentWriter();
            var rateLimiter = RateLimiterFactory.Create(console, options.MaxRate, options.BatchSize, options.BatchPauseTime);
            var targetQueueName = !string.IsNullOrEmpty(options.TargetQueueName) ? options.TargetQueueName : options.QueueName;

            var totalCount = (int)sourceChannel.MessageCount(options.QueueName);

            var skip = Math.Max(options.Skip, 0);
            if (skip > 0)
                totalCount -= Math.Min(skip, totalCount);

            if (options.MaxCount.HasValue && options.MaxCount.Value < totalCount)
                totalCount = options.MaxCount.Value;

            consoleWriter.WriteLine($"Shoveling {totalCount} message{(totalCount != 1 ? "s" : "")} (actual number may differ if queue has active consumers or publishers)");
            var messageCount = 0;

            using (var progressBar = new ProgressBar(console, totalCount))
            {
                // Perform the skips outside of the rate limiter
                while (skip > 0 && !console.Cancelled)
                {
                    var result = sourceChannel.BasicGet(options.QueueName, false);
                    if (result == null)
                        // No more messages on the queue
                        return;

                    skip--;
                }

                
                var hasMessage = true;

                while (!console.Cancelled && hasMessage && (!options.MaxCount.HasValue || messageCount < options.MaxCount.Value))
                {
                    rateLimiter.Execute(() =>
                    {
                        if (console.Cancelled)
                            return;
                        
                        var result = sourceChannel.BasicGet(options.QueueName, false);
                        if (result == null)
                        {
                            // No more messages on the queue
                            hasMessage = false;
                            return;
                        }

                        // Since RabbitMQ client 6 we need to copy the body before calling another channel method
                        // like BasicPublish, or the published body will be corrupted if sourceChannel and targetChannel are the same
                        var bodyCopy = result.Body.ToArray();

                        targetChannel.BasicPublish("", targetQueueName, result.BasicProperties, bodyCopy);
                        messageCount++;

                        if (options.RemoveMessages)
                            sourceChannel.BasicAck(result.DeliveryTag, false);

                        // ReSharper disable once AccessToDisposedClosure
                        progressBar.Report(messageCount);
                    });
                }
            }

            consoleWriter.WriteLine($"{messageCount} message{(messageCount != 1 ? "s" : "")} shoveled.");
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
    }
}
