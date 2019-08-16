namespace ExampleLib
{
    /// <summary>
    /// Since the examples do not run as a service, this interface provides a way
    /// for the implementation to signal that it has finished and the example can be closed.
    /// </summary>
    public interface IExampleState
    {
        /// <summary>
        /// Signals the Program that the example has finished and the application can be closed.
        /// </summary>
        void Done();
    }
}
