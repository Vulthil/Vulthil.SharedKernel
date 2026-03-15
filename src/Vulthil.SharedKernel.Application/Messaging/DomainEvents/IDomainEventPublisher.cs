using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application.Messaging.DomainEvents;

/// <summary>
/// Publishes domain events to their registered handlers.
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publishes a domain event by its runtime type to all registered handlers.
    /// </summary>
    /// <param name="notification">The domain event to publish.</param>
    Task PublishAsync(object notification) => PublishAsync(notification, CancellationToken.None);

    /// <summary>
    /// Publishes a domain event by its runtime type to all registered handlers.
    /// </summary>
    /// <param name="notification">The domain event to publish.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task PublishAsync(object notification, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a strongly-typed domain event to all registered handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of domain event to publish.</typeparam>
    /// <param name="notification">The domain event to publish.</param>
    Task PublishAsync<TNotification>(TNotification notification) where TNotification : IDomainEvent
        => PublishAsync(notification, CancellationToken.None);

    /// <summary>
    /// Publishes a strongly-typed domain event to all registered handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of domain event to publish.</typeparam>
    /// <param name="notification">The domain event to publish.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken) where TNotification : IDomainEvent;
}
