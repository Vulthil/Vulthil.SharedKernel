namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

/// <summary>
/// Relays a fetched outbox message to its sink. The <see cref="OutboxProcessor"/> routes each row to the
/// registered dispatcher whose <see cref="Handles"/> returns <see langword="true"/> for the row's
/// <see cref="OutboxDestination"/>, so a single outbox table can feed several sinks (in-process domain events,
/// broker publishes/sends) that coexist in one application.
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>Whether this dispatcher relays messages for the given <paramref name="destination"/>.</summary>
    /// <param name="destination">The message's destination.</param>
    bool Handles(OutboxDestination destination);

    /// <summary>Relays <paramref name="message"/> to its sink.</summary>
    /// <param name="message">The fetched outbox message data.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken);
}
