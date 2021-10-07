using Tapeti.Config;

namespace Tapeti.Serilog
{
    /// <summary>
    /// Stores the IDiagnosticContext for a message handler.
    /// </summary>
    public class DiagnosticContextPayload : IMessageContextPayload
    {
        /// <summary>
        /// Initializes a DiagnosticContext payload.
        /// </summary>
        public DiagnosticContextPayload(IDiagnosticContext diagnosticContext)
        {
            DiagnosticContext = diagnosticContext;
        }

        /// <summary>
        /// The diagnostic context for the current message handler.
        /// </summary>
        public IDiagnosticContext DiagnosticContext { get; }
    }
}
