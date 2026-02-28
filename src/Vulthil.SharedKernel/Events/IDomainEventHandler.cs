namespace Vulthil.SharedKernel.Events;

/// <summary>
/// Defines a handler that processes a specific domain event type.
/// </summary>
/// <typeparam name="TDomainEvent">The type of domain event to handle.</typeparam>
public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    /// <summary>
    /// Handles the specified domain event.
    /// </summary>
    /// <param name="notification">The domain event to handle.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleAsync(TDomainEvent notification, CancellationToken cancellationToken = default);
}
