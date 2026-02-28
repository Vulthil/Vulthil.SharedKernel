namespace Vulthil.Messaging;

/// <summary>
/// Represents the message transport responsible for starting consumer connections.
/// </summary>
public interface ITransport
{
    /// <summary>
    /// Starts the transport, establishing connections and beginning message consumption.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the asynchronous startup operation.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);
}
