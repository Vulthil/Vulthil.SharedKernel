using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// One registered consumer or request consumer, as a <see cref="DeliveryDispatcher"/> runs it for a delivered message.
/// <see cref="DeliveryHandlerFactory"/> builds one per registration; a transport keeps them in a
/// <see cref="MessageExecutionRegistry{THandler}"/> and hands a plan's handlers to the dispatcher.
/// </summary>
public sealed class DeliveryHandler
{
    internal DeliveryHandler(string identity, HandlerKind kind, RetryPolicyDefinition? retryPolicy, DeliveryHandlerInvoker invoke)
    {
        Identity = identity;
        Kind = kind;
        RetryPolicy = retryPolicy;
        Invoke = invoke;
    }

    /// <summary>
    /// Gets the handler's stable identity: the consumer's CLR full name and the registered message type's full name,
    /// joined by a colon. A transport that redelivers later carries these identities on the redelivery so only the
    /// handlers that failed run again (see <see cref="DeliveryOutcome.RedeliveryHandlerIdentities"/>).
    /// </summary>
    public string Identity { get; }

    /// <summary>Gets the consumer contract the handler implements.</summary>
    public HandlerKind Kind { get; }

    /// <summary>
    /// Gets the handler's effective retry policy — its own, or the queue default — or <see langword="null"/> when the
    /// handler fails for good on its first failure. Always <see langword="null"/> for a request consumer, which
    /// replies with an <see cref="RpcFault"/> instead of retrying.
    /// </summary>
    public RetryPolicyDefinition? RetryPolicy { get; }

    internal DeliveryHandlerInvoker Invoke { get; }

    internal static string BuildIdentity(Type consumerType, Type messageType)
        => $"{consumerType.FullName}:{messageType.FullName}";
}

/// <summary>
/// Runs one attempt of a handler: builds the context through the port, runs the consume pipeline, and for a request
/// consumer sends the reply.
/// </summary>
internal delegate Task<DeliveryAttempt> DeliveryHandlerInvoker(IServiceProvider services, object message, IDeliveryPort port, int retryCount);
