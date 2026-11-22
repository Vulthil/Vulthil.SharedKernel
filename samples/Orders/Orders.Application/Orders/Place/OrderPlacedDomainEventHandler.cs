using Microsoft.Extensions.Logging;
using Orders.Domain.Orders.Events;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.SharedKernel.Events;

namespace Orders.Application.Orders.Place;

/// <summary>
/// Runs when the outbox relay dispatches <see cref="OrderPlacedDomainEvent"/>, inside the relay's transaction, so
/// the transactional outbox captures the publish and relays it once that transaction commits.
/// </summary>
public sealed class OrderPlacedDomainEventHandler(ILogger<OrderPlacedDomainEventHandler> logger, IPublisher publisher) : IDomainEventHandler<OrderPlacedDomainEvent>
{
    public async Task HandleAsync(OrderPlacedDomainEvent notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Order {OrderId} was placed", notification.OrderId.Value);
        await publisher.PublishAsync(new OrderPlacedIntegrationEvent(notification.OrderId.Value), cancellationToken: cancellationToken);
    }
}
