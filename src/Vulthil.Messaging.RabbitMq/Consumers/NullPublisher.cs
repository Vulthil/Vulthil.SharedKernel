using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// A placeholder <see cref="IPublisher"/> used by <see cref="MessageContext"/> snapshots
/// (e.g. <see cref="Vulthil.Messaging.Abstractions.Consumers.Fault{TMessage}.OriginalContext"/>) where no live
/// transport publisher is bound. Calling <see cref="PublishAsync"/> on a snapshot is a programmer error.
/// </summary>
internal sealed class NullPublisher : IPublisher
{
    public static readonly NullPublisher Instance = new();

    private NullPublisher() { }

    public Task PublishAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TMessage : notnull
        => throw new InvalidOperationException(
            "This message context is a snapshot (e.g. a fault envelope) and is not bound to a live publisher.");
}
