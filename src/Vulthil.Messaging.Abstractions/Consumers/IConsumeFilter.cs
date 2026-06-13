namespace Vulthil.Messaging.Abstractions.Consumers;

/// <summary>
/// Delegate that invokes the next stage in the consume pipeline.
/// </summary>
/// <typeparam name="TMessage">The message type being consumed.</typeparam>
/// <param name="context">The message context for the current delivery.</param>
/// <returns>A task representing the asynchronous operation.</returns>
public delegate Task ConsumeDelegate<in TMessage>(IMessageContext<TMessage> context)
    where TMessage : notnull;

/// <summary>
/// A filter in the consume pipeline. Filters wrap the consumer invocation, allowing
/// cross-cutting concerns (logging, validation, scoped resource management, telemetry, etc.)
/// to be composed without modifying transport or consumer code.
/// </summary>
/// <typeparam name="TMessage">The message type the filter applies to.</typeparam>
/// <remarks>
/// Filters are composed in registration order: the first registered filter is the
/// outermost. A filter may short-circuit the pipeline by not invoking the
/// <c>next</c> delegate, e.g. to reject a message based on a validation rule.
/// Filters are resolved per delivery from the same scope as the consumer, so they may
/// depend on scoped services (e.g. <c>DbContext</c>, scoped <c>ILogger&lt;T&gt;</c>).
/// </remarks>
public interface IConsumeFilter<TMessage> where TMessage : notnull
{
    /// <summary>
    /// Processes the message, optionally invoking <paramref name="next"/> to continue the pipeline.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="next">The next stage of the pipeline. The terminal stage invokes the consumer.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConsumeAsync(IMessageContext<TMessage> context, ConsumeDelegate<TMessage> next);
}
