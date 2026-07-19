using RabbitMQ.Client.Events;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// Transport-internal record describing how to invoke a single consumer or request consumer
/// for a delivered message. Built once at registration time by <see cref="MessageHandlerFactory"/>;
/// the dispatch closure captures the typed consumer/message parameters so the worker can invoke
/// it without further reflection.
/// </summary>
internal sealed record MessageHandler
{
    /// <summary>
    /// The effective retry policy the worker applies when this handler throws — the registration's own policy
    /// when set, otherwise the queue's default, resolved once by the registry; <see langword="null"/> means the
    /// handler fails terminally on its first failure. Always <see langword="null"/> for request consumers,
    /// which reply with an RPC fault instead of retrying.
    /// </summary>
    public RetryPolicyDefinition? RetryPolicy { get; init; }

    /// <summary>
    /// Stable identity of this handler: the consumer's CLR full name paired with the registered message type's
    /// full name. Stamped on delayed-retry re-publishes so the re-delivery re-dispatches only the handlers that
    /// failed; stable across processes for as long as the consumer type keeps its name.
    /// </summary>
    public required string Identity { get; init; }

    /// <summary>The consumer contract the handler implements.</summary>
    public required HandlerKind Kind { get; init; }

    /// <summary>Builds the stable handler identity for a consumer/registered-message type pair.</summary>
    internal static string BuildIdentity(Type consumerType, Type messageType)
        => $"{consumerType.FullName}:{messageType.FullName}";

    /// <summary>
    /// Dispatches a deserialized message through the consume pipeline and (for RPC) publishes the response through
    /// the supplied <see cref="GatedPublisher"/>, which serializes the write with the worker's other channel
    /// operations. Consumer-kind handlers ignore the publisher parameter. The envelope is non-null on the standard
    /// receive path (Vulthil-produced messages) and null on the bare-JSON compat path (external producers); the
    /// closure picks the appropriate <c>MessageContextFactory.CreateContext</c> overload.
    /// </summary>
    public required Func<IServiceProvider, object, BasicDeliverEventArgs, MessageEnvelope?, GatedPublisher, CancellationToken, Task> DispatchAsync { get; init; }
}
