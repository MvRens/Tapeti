using System;
using CommandLine;
using RabbitMQ.Client;
using Tapeti.Cmd.ConsoleHelper;

namespace Tapeti.Cmd.Verbs
{
    public class BaseConnectionOptions
    {
        [Option('h', "host", HelpText = "(Default: localhost) Hostname of the RabbitMQ server. Can also be set using the TAPETI_HOST environment variable.")]
        public string Host { get; set; }

        [Option("port", HelpText = "(Default: 5672) AMQP port of the RabbitMQ server. Can also be set using the TAPETI_PORT environment variable.")]
        public int? Port { get; set; }

        [Option('v', "virtualhost", HelpText = "(Default: /) Virtual host used for the RabbitMQ connection. Can also be set using the TAPETI_VIRTUALHOST environment variable.")]
        public string VirtualHost { get; set; }

        [Option('u', "username", HelpText = "(Default: guest) Username used to connect to the RabbitMQ server. Can also be set using the TAPETI_USERNAME environment variable.")]
        public string Username { get; set; }

        [Option('p', "password", HelpText = "(Default: guest) Password used to connect to the RabbitMQ server. Can also be set using the TAPETI_PASSWORD environment variable.")]
        public string Password { get; set; }


        public ConnectionFactory CreateConnectionFactory(IConsole console)
        {
            var consoleWriter = console.GetPermanentWriter();
            consoleWriter.WriteLine("Using connection parameters:");
            
            var factory = new ConnectionFactory
            {
                HostName    = GetOptionOrEnvironmentValue(consoleWriter, "  Host         : ", Host, "TAPETI_HOST", "localhost"),
                Port        = GetOptionOrEnvironmentValue(consoleWriter, "  Port         : ", Port, "TAPETI_PORT", 5672),
                VirtualHost = GetOptionOrEnvironmentValue(consoleWriter, "  Virtual host : ", VirtualHost, "TAPETI_VIRTUALHOST", "/"),
                UserName    = GetOptionOrEnvironmentValue(consoleWriter, "  Username     : ", Username, "TAPETI_USERNAME", "guest"),
                Password    = GetOptionOrEnvironmentValue(consoleWriter, "  Password     : ", Password, "TAPETI_PASSWORD", "guest", true)
            };
            
            consoleWriter.WriteLine("");
            return factory;
        }


        private static string GetOptionOrEnvironmentValue(IConsoleWriter consoleWriter, string consoleDisplayName, string optionValue, string environmentName, string defaultValue, bool hideValue = false)
        {
            string GetDisplayValue(string value)
            {
                return hideValue
                    ? "<hidden>"
                    : value;
            }
            
            if (!string.IsNullOrEmpty(optionValue))
            {
                consoleWriter.WriteLine($"{consoleDisplayName}{GetDisplayValue(optionValue)} (from command-line)");
                return optionValue;
            }

            var environmentValue = Environment.GetEnvironmentVariable(environmentName);
            if (!string.IsNullOrEmpty(environmentValue))
            {
                consoleWriter.WriteLine($"{consoleDisplayName}{GetDisplayValue(environmentValue)} (from environment variable)");
                return environmentValue;
            }

            consoleWriter.WriteLine($"{consoleDisplayName}{GetDisplayValue(defaultValue)} (default)");
            return defaultValue;
        }


        private static int GetOptionOrEnvironmentValue(IConsoleWriter consoleWriter, string consoleDisplayName, int? optionValue, string environmentName, int defaultValue)
        {
            if (optionValue.HasValue)
            {
                consoleWriter.WriteLine($"{consoleDisplayName}{optionValue} (from command-line)");
                return optionValue.Value;
            }
        

            var environmentValue = Environment.GetEnvironmentVariable(environmentName);
            if (!string.IsNullOrEmpty(environmentValue) && int.TryParse(environmentValue, out var environmentIntValue))
            {
                consoleWriter.WriteLine($"{consoleDisplayName}{environmentIntValue} (from environment variable)");
                return environmentIntValue;
            }

            consoleWriter.WriteLine($"{consoleDisplayName}{defaultValue} (default)");
            return defaultValue;
        }

    }
}
