using Microsoft.Extensions.Logging;
using Orders.Domain.Orders.Events;
using Vulthil.SharedKernel.Events;

namespace Orders.Application.Orders.ChangeDescription;

public sealed class OrderDescriptionChangedDomainEventHandler(ILogger<OrderDescriptionChangedDomainEventHandler> logger) : IDomainEventHandler<OrderDescriptionChangedDomainEvent>
{
    public Task HandleAsync(OrderDescriptionChangedDomainEvent notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Order {OrderId} is now described as {Description}", notification.OrderId.Value, notification.Description);
        return Task.CompletedTask;
    }
}
