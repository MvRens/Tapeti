using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace Tapeti.Serilog.Default
{
    /// <summary>
    /// Implements the IDiagnosticContext interface for a Serilog ILogger.
    /// </summary>
    public class DiagnosticContext : IDiagnosticContext
    {
        private readonly global::Serilog.ILogger logger;
        private readonly List<LogEventProperty> properties = new List<LogEventProperty>();

        
        /// <summary>
        /// Creates a new instance of a DiagnosticContext
        /// </summary>
        /// <param name="logger">The Serilog ILogger which will be enriched</param>
        public DiagnosticContext(global::Serilog.ILogger logger)
        {
            this.logger = logger;
        }
        

        public void Set(string propertyName, object value, bool destructureObjects = false)
        {
            if (logger.BindProperty(propertyName, value, destructureObjects, out var logEventProperty))
                properties.Add(logEventProperty);
        }


        /// <summary>
        /// Returns a Serilog ILogger which is enriched with the properties set by this DiagnosticContext
        /// </summary>
        public global::Serilog.ILogger GetEnrichedLogger()
        {
            return properties.Count > 0
                ? logger.ForContext(new LogEventPropertiesEnricher(properties))
                : logger;
        }


        private class LogEventPropertiesEnricher : ILogEventEnricher
        {
            private readonly IEnumerable<LogEventProperty> properties;

            public LogEventPropertiesEnricher(IEnumerable<LogEventProperty> properties)
            {
                this.properties = properties;
            }
            
            
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                foreach (var property in properties)
                    logEvent.AddOrUpdateProperty(property);
            }
        }
    }
}
