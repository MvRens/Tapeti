namespace Tapeti.Serilog
{
    /// <summary>
    /// Collects diagnostic information for message handler logging when using the
    /// MessageHandlerLogging middleware.
    /// </summary>
    /// <remarks>
    /// Similar to IDiagnosticContext in Serilog.Extensions.Hosting but slightly extended.
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

        /// <summary>
        /// Resets the timer which is used to monitor how long a message handler takes to complete.
        /// Useful for example when a message handler is throttled by a rate limiter in the message
        /// handler method and you want to measure only the time taken after it is allowed to start.
        /// </summary>
        /// <param name="addToContext">If true, the time taken until this reset is added to this diagnostic context as an incrementally named property for logging purposes. The value will be the time in milliseconds.</param>
        /// <param name="propertyNamePrefix">The prefix for the property name(s) when addToContext is true. The number of times ResetStopwatch is called will be appended (stopwatchReset1, stopwatchReset2, etc).</param>
        void ResetStopwatch(bool addToContext = true, string propertyNamePrefix = "stopwatchReset");
    }
}
