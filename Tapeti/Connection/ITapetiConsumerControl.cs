using System.Threading.Tasks;

namespace Tapeti.Connection;

/// <summary>
/// Provides access to control a resilient consumer.
/// </summary>
public interface ITapetiConsumerControl
{
    /// <summary>
    /// Stops the consumer.
    /// </summary>
    Task Cancel();
}
