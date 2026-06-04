namespace Vulthil.Messaging.Transport;

/// <summary>
/// Pairs a transport-specific dispatch handler with the consumer contract it implements. Returned by
/// <see cref="IMessageHandlerFactory{THandler}"/> so <see cref="MessageExecutionRegistry{THandler}"/> can
/// enforce request-consumer uniqueness without inspecting the opaque <typeparamref name="THandler"/> value.
/// </summary>
/// <typeparam name="THandler">The transport-specific handler type produced by the factory.</typeparam>
/// <param name="Handler">The transport-specific handler that runs when a matching message is delivered.</param>
/// <param name="Kind">The consumer contract the handler implements.</param>
public sealed record HandlerEntry<THandler>(THandler Handler, HandlerKind Kind)
    where THandler : notnull;
