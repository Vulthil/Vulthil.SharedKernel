using Vulthil.SharedKernel.Outbox;

namespace Vulthil.Messaging.Outbox;

/// <summary>
/// Holds the outbox relay until the message transport has declared its subscriber topology, so a publish relayed at
/// startup is routed to the subscriber queues instead of being dropped as unroutable. Without this gate the
/// commit-time trigger can wake the relay before the broker bindings exist.
/// </summary>
internal sealed class TransportReadinessOutboxGate(ITransport transport) : IOutboxRelayGate
{
    public Task WaitUntilReadyAsync(CancellationToken cancellationToken) =>
        transport.WaitUntilReadyAsync(cancellationToken);
}
