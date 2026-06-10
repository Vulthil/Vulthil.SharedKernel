namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

/// <summary>
/// A readiness gate the outbox relay awaits once before its first processing pass. It lets a downstream sink defer
/// the relay until its delivery channel is ready — for example, the message-broker sink waits for the transport to
/// declare its subscriber topology, so a relayed publish is not lost by being sent before the subscriber queues are
/// bound. When no gate is registered the relay starts immediately.
/// </summary>
public interface IOutboxRelayGate
{
    /// <summary>
    /// Completes when the gate's downstream channel is ready to accept relayed messages.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the relay may begin processing.</returns>
    Task WaitUntilReadyAsync(CancellationToken cancellationToken);
}
