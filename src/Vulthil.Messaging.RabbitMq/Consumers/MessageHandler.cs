using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Envelope;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// Transport-internal record describing how to invoke a single consumer or request consumer
/// for a delivered message. Built once at registration time by <see cref="MessageHandlerFactory"/>;
/// the dispatch closure captures the typed consumer/message parameters so the worker can invoke
/// it without further reflection.
/// </summary>
internal sealed record MessageHandler
{
    /// <summary>The retry policy applied by the worker when this handler's plan throws.</summary>
    public RetryPolicyDefinition? RetryPolicy { get; init; }

    /// <summary>The consumer contract the handler implements.</summary>
    public required HandlerKind Kind { get; init; }

    /// <summary>
    /// Dispatches a deserialized message through the consume pipeline and (for RPC) publishes the response on the supplied channel.
    /// Consumer-kind handlers ignore the channel parameter. The envelope is non-null on the standard receive path
    /// (Vulthil-produced messages) and null on the bare-JSON compat path (external producers); the closure picks the
    /// appropriate <c>MessageContext.CreateContext</c> overload.
    /// </summary>
    public required Func<IServiceProvider, object, BasicDeliverEventArgs, MessageEnvelope?, IChannel, CancellationToken, Task> DispatchAsync { get; init; }
}
