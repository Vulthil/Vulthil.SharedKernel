namespace Vulthil.Messaging;

/// <summary>
/// Represents the message transport responsible for starting and stopping message consumption.
/// </summary>
public interface ITransport
{
    /// <summary>
    /// Starts the transport, establishing connections and beginning message consumption.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the asynchronous startup operation.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes once the transport has finished its startup pass — for a broker transport, once exchanges, queues,
    /// and subscriber bindings are declared, so a message published afterwards is routed to the subscriber queues.
    /// Background publishers (such as the outbox relay) should await this before their first publish to avoid losing
    /// messages sent before subscriber topology exists. The default implementation is ready immediately.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the transport is ready to route published messages.</returns>
    Task WaitUntilReadyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Stops message consumption. When the returned task completes, no new message is delivered and the messages that
    /// were being handled have finished (a transport may bound that wait); messages that were not delivered stay with
    /// the broker. A stopped transport consumes again after <see cref="StartAsync"/>. The default implementation does
    /// nothing, for a transport without background consumers.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
