namespace Vulthil.Messaging.Transport;

/// <summary>
/// Delegate that invokes the next stage in the publish/send pipeline. The terminal stage performs the actual
/// transport publish or send.
/// </summary>
/// <param name="context">The publish filter context for the current outgoing message.</param>
/// <returns>A task representing the asynchronous operation.</returns>
public delegate Task PublishFilterDelegate(PublishFilterContext context);

/// <summary>
/// A filter in the publish/send pipeline. Filters wrap the transport send, allowing cross-cutting concerns
/// (transactional outbox capture, logging, validation, telemetry, …) to be composed without modifying transport
/// or caller code.
/// </summary>
/// <remarks>
/// Filters are composed in registration order: the first registered filter is the outermost. A filter may
/// short-circuit the pipeline by not invoking the <c>next</c> delegate. Filters are resolved from the caller's
/// scope, so they may depend on scoped services (e.g. <c>DbContext</c>). The same filter sees both publishes and
/// sends; use <see cref="PublishFilterContext.Kind"/> to distinguish them.
/// </remarks>
public interface IPublishFilter
{
    /// <summary>
    /// Processes the outgoing message, optionally invoking <paramref name="next"/> to continue the pipeline.
    /// </summary>
    /// <param name="context">The publish filter context.</param>
    /// <param name="next">The next stage of the pipeline. The terminal stage performs the transport send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync(PublishFilterContext context, PublishFilterDelegate next);
}
