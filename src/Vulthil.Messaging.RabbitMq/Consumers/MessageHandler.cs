using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// Transport-internal record describing how to invoke a single consumer or request consumer
/// for a delivered message. Built once at registration time by <see cref="MessageHandlerFactory"/>;
/// the dispatch closure captures the typed consumer/message parameters so the worker can invoke
/// it without further reflection.
/// </summary>
internal sealed record MessageHandler
{
    /// <summary>The routing key (or topic pattern) the handler was registered with. Used for diagnostics; the broker is authoritative for delivery.</summary>
    public required string RoutingKey { get; init; }

    /// <summary>The retry policy applied by the worker when this handler throws.</summary>
    public RetryPolicyDefinition? RetryPolicy { get; init; }

    /// <summary>The consumer contract the handler implements.</summary>
    public required HandlerKind Kind { get; init; }

    /// <summary>
    /// Dispatches a deserialized message through the consume pipeline and (for RPC) publishes the response on the supplied channel.
    /// Consumer-kind handlers ignore the channel parameter.
    /// </summary>
    public required Func<IServiceProvider, object, BasicDeliverEventArgs, IChannel, CancellationToken, Task> DispatchAsync { get; init; }
}
