using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// In-memory messaging test harness. Captures every message produced and consumed through the in-memory
/// transport so tests can assert on messaging behaviour with no broker, and lets a test stand in for an
/// external service by handling published messages or responding to requests.
/// </summary>
/// <remarks>
/// The harness dispatches synchronously: by the time a publish, send, or request call completes, every
/// consumer (and registered <see cref="Handle{TMessage}"/>/<see cref="Respond{TRequest, TResponse}"/> stub)
/// it triggered has run, so assertions need no polling. A one-way consumer that throws is retried per its
/// configured policy and, once the attempts are exhausted, a <c>Fault&lt;T&gt;</c> is published — the publish or
/// send itself still completes — mirroring the broker transport; a request consumer's exception is surfaced as a
/// failed request result.
/// </remarks>
public interface ITestHarness
{
    /// <summary>Gets the messages published via <c>IPublisher.PublishAsync</c> that are assignable to <typeparamref name="TMessage"/>, in order.</summary>
    /// <typeparam name="TMessage">The message type to filter by.</typeparam>
    IReadOnlyList<CapturedMessage<TMessage>> Published<TMessage>() where TMessage : notnull;

    /// <summary>Gets the messages sent via <c>ISendEndpoint.SendAsync</c> that are assignable to <typeparamref name="TMessage"/>, in order.</summary>
    /// <typeparam name="TMessage">The message type to filter by.</typeparam>
    IReadOnlyList<CapturedMessage<TMessage>> Sent<TMessage>() where TMessage : notnull;

    /// <summary>Gets the messages delivered to a registered consumer that are assignable to <typeparamref name="TMessage"/>, in order. A message handled by several consumers is captured once per consumer.</summary>
    /// <typeparam name="TMessage">The message type to filter by.</typeparam>
    IReadOnlyList<CapturedMessage<TMessage>> Consumed<TMessage>() where TMessage : notnull;

    /// <summary>Gets the requests issued via <c>IRequester.RequestAsync</c> that are assignable to <typeparamref name="TMessage"/>, in order.</summary>
    /// <typeparam name="TMessage">The request type to filter by.</typeparam>
    IReadOnlyList<CapturedMessage<TMessage>> Requested<TMessage>() where TMessage : notnull;

    /// <summary>
    /// Registers a stub that runs whenever a <typeparamref name="TMessage"/> is published or sent, in addition to
    /// any registered consumers. Use it to stand in for a downstream service — the stub can publish or send
    /// follow-up messages through its context.
    /// </summary>
    /// <typeparam name="TMessage">The message type to react to.</typeparam>
    /// <param name="handler">The stub invoked with the delivered message context.</param>
    void Handle<TMessage>(Func<IMessageContext<TMessage>, Task> handler) where TMessage : notnull;

    /// <summary>
    /// Registers a stub that answers requests of type <typeparamref name="TRequest"/> with a
    /// <typeparamref name="TResponse"/>, standing in for an external request consumer. A registered responder
    /// takes precedence over a real request consumer for the same request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type to answer.</typeparam>
    /// <typeparam name="TResponse">The response type to return.</typeparam>
    /// <param name="responder">The stub invoked with the request context to produce the response.</param>
    void Respond<TRequest, TResponse>(Func<IMessageContext<TRequest>, TResponse> responder)
        where TRequest : notnull
        where TResponse : notnull;

    /// <summary>Clears all captured published, sent, consumed, and requested messages. Registered stubs are retained.</summary>
    void Clear();
}
