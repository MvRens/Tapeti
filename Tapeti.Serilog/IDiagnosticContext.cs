namespace Tapeti.Serilog
{
    /// <summary>
    /// Collects diagnostic information for message handler logging when using the
    /// MessageHandlerLogging middleware.
    /// </summary>
    /// <remarks>
    /// This is a one-to-one copy of the IDiagnosticContext in Serilog.Extensions.Hosting which
    /// saves a reference to that package while allowing similar usage within Tapeti message handlers.
    /// </remarks>
    public interface IDiagnosticContext
    {
        /// <summary>
        /// Set the specified property on the current diagnostic context. The property will be collected
        /// and attached to the event emitted at the completion of the context.
        /// </summary>
        /// <param name="propertyName">The name of the property. Must be non-empty.</param>
        /// <param name="value">The property value.</param>
        /// <param name="destructureObjects">If true, the value will be serialized as structured
        /// data if possible; if false, the object will be recorded as a scalar or simple array.</param>
        void Set(string propertyName, object value, bool destructureObjects = false);
    }
}
