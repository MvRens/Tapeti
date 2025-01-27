using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog.Events;
using Tapeti.Config;
using Tapeti.Serilog.Default;

namespace Tapeti.Serilog.Middleware
{
    /// <summary>
    /// Implements the message handler logging middleware which provides an IDiagnosticContext for
    /// the message handler and logs the result.
    /// </summary>
    public class MessageHandlerLoggingMessageMiddleware : IControllerMessageMiddleware
    {
        private readonly double elapsedWarningTreshold;
        private readonly LogEventLevel defaultLevel;

        /// <summary>
        /// Creates a new instance of the message handler logging middleware which provides an IDiagnosticContext
        /// for the message handler and logs the result.
        /// </summary>
        /// <param name="elapsedWarningTreshold">The time (in milliseconds) a message handler is allowed to run without a warning being logged</param>
        /// <param name="defaultLevel">The default log level when a message handler completes within the elapsedWarningTreshold</param>
        public MessageHandlerLoggingMessageMiddleware(double elapsedWarningTreshold = 500, LogEventLevel defaultLevel = LogEventLevel.Debug)
        {
            this.elapsedWarningTreshold = elapsedWarningTreshold;
            this.defaultLevel = defaultLevel;
        }
        
        /// <inheritdoc />
        public async ValueTask Handle(IMessageContext context, Func<ValueTask> next)
        {
            var logger = context.Config.DependencyResolver.Resolve<global::Serilog.ILogger>();

            var stopwatch = new Stopwatch();
            var diagnosticContext = new DiagnosticContext(logger, stopwatch);
            context.Store(new DiagnosticContextPayload(diagnosticContext));
            
            stopwatch.Start();
            
            await next().ConfigureAwait(false);


            stopwatch.Stop();

            
            var controllerName = "Unknown";
            var methodName = "Unknown";

            if (context.TryGet<ControllerMessageContextPayload>(out var controllerMessageContextPayload))
            {
                controllerName = controllerMessageContextPayload.Binding.Controller.Name;
                methodName = controllerMessageContextPayload.Binding.Method.Name;
            }
            
            var level = stopwatch.ElapsedMilliseconds > elapsedWarningTreshold ? LogEventLevel.Warning : defaultLevel;
            var enrichedLogger = diagnosticContext.GetEnrichedLogger();
            
            enrichedLogger.Write(level, "Tapeti: {controller}.{method} completed in {elapsedMilliseconds} ms", 
                controllerName, methodName, stopwatch.ElapsedMilliseconds);
        }
    }
}
