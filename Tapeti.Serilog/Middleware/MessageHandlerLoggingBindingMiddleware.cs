using System;
using System.Linq;
using Serilog.Events;
using Tapeti.Config;

namespace Tapeti.Serilog.Middleware
{
    /// <summary>
    /// Implements the middleware which binds any IDiagnosticContext parameter in message handlers.
    /// </summary>
    public class MessageHandlerLoggingBindingMiddleware : IControllerBindingMiddleware
    {
        private readonly IControllerMessageMiddleware controllerMessageMiddleware;


        /// <summary>
        /// Creates a new instance of the middleware which binds any IDiagnosticContext parameter in message handlers.
        /// </summary>
        /// <param name="elapsedWarningTreshold"></param>
        /// <param name="defaultLevel"></param>
        public MessageHandlerLoggingBindingMiddleware(double elapsedWarningTreshold = 500, LogEventLevel defaultLevel = LogEventLevel.Debug)
        {
            controllerMessageMiddleware = new MessageHandlerLoggingMessageMiddleware(elapsedWarningTreshold, defaultLevel);
        }
        
        
        /// <inheritdoc />
        public void Handle(IControllerBindingContext context, Action next)
        {
            RegisterDiagnosticContextParameter(context);
            
            // All state is contained within the message context, using a single middleware instance is safe
            context.Use(controllerMessageMiddleware);

            next();
        }
        
        
        private static void RegisterDiagnosticContextParameter(IControllerBindingContext context)
        {
            foreach (var parameter in context.Parameters.Where(p => !p.HasBinding && p.Info.ParameterType == typeof(IDiagnosticContext)))
                parameter.SetBinding(DiagnosticContextFactory);
        }


        private static object DiagnosticContextFactory(IMessageContext context)
        {
            return context.TryGet<DiagnosticContextPayload>(out var diagnosticContextPayload)
                ? diagnosticContextPayload.DiagnosticContext
                : null;
        }
    }
}
