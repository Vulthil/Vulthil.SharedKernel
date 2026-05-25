using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.RabbitMq.Sending;

/// <summary>
/// A placeholder <see cref="ISendEndpointProvider"/> used by <see cref="Consumers.MessageContext"/> snapshots
/// (e.g. <see cref="Vulthil.Messaging.Abstractions.Consumers.Fault{TMessage}.OriginalContext"/>) where no live
/// transport is bound. Calling <see cref="GetSendEndpointAsync"/> on a snapshot is a programmer error.
/// </summary>
internal sealed class NullSendEndpointProvider : ISendEndpointProvider
{
    public static readonly NullSendEndpointProvider Instance = new();

    private NullSendEndpointProvider() { }

    public ValueTask<ISendEndpoint> GetSendEndpointAsync(Uri address, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "This message context is a snapshot (e.g. a fault envelope) and is not bound to a live transport.");
}
