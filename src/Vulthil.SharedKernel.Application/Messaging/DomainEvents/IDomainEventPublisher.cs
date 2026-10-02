using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application.Messaging.DomainEvents;

/// <summary>
/// Publishes domain events to their registered handlers.
/// </summary>
/// <remarks>
/// A publish dispatches by the event's runtime type. Every handler registered for that type runs in registration
/// order, inside the type's <see cref="Pipeline.IDomainEventPipelineHandler{TDomainEvent}"/>s, which wrap the whole set
/// of handlers. A failed handler does not stop the others: once every handler has run, their failures are thrown
/// together in one <see cref="AggregateException"/>, even when only one handler failed. A cancellation of the caller's
/// token stops the publish instead: no further handler starts, and the <see cref="OperationCanceledException"/> is
/// thrown unwrapped, even after an earlier handler failed. A handler's own <see cref="OperationCanceledException"/>
/// while the caller's token is still live is an ordinary failure.
/// </remarks>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publishes a domain event by its runtime type to all registered handlers.
    /// </summary>
    /// <param name="notification">The domain event to publish.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notification"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="notification"/> does not implement <see cref="IDomainEvent"/>.</exception>
    /// <exception cref="AggregateException">One or more handlers failed; every handler still ran.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task PublishAsync(object notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a strongly-typed domain event to all registered handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of domain event to publish.</typeparam>
    /// <param name="notification">The domain event to publish.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notification"/> is <see langword="null"/>.</exception>
    /// <exception cref="AggregateException">One or more handlers failed; every handler still ran.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : IDomainEvent;
}
