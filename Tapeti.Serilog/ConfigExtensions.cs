using Serilog.Events;
using Tapeti.Config;
using Tapeti.Serilog.Middleware;

// ReSharper disable UnusedMember.Global - public API

namespace Tapeti.Serilog
{
    /// <summary>
    /// ITapetiConfigBuilder extension for enabling message handler logging.
    /// </summary>
    public static class ConfigExtensions
    {
        /// <summary>
        /// Enables message handler logging.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="elapsedWarningTreshold">The time (in milliseconds) a message handler is allowed to run without a warning being logged</param>
        /// <param name="defaultLevel">The default log level when a message handler completes within the elapsedWarningTreshold</param>
        /// <returns></returns>
        public static ITapetiConfigBuilder WithMessageHandlerLogging(this ITapetiConfigBuilder config, 
            double elapsedWarningTreshold = 500, LogEventLevel defaultLevel = LogEventLevel.Debug)
        {
            config.Use(new MessageHandlerLoggingBindingMiddleware(elapsedWarningTreshold, defaultLevel));
            return config;
        }
    }
}
