using Microsoft.EntityFrameworkCore;
using Orders.Application.Shipping;
using Orders.Domain.Orders;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Application.Orders.Ship;

public sealed record ShipOrderCommand(Guid OrderId, string Recipient) : ITransactionalCommand;

/// <summary>
/// Marks the order shipped and sends <see cref="CreateShipment"/> to the Shipments service's queue. The command is
/// transactional, so the outbox captures the send in the same transaction as the order, and the send reaches the
/// broker only after that transaction commits. Shipping an order twice throws <see cref="OrderAlreadyShippedException"/>.
/// </summary>
public sealed class ShipOrderCommandHandler(IOrdersDbContext dbContext, ISendEndpointProvider sendEndpoints, TimeProvider timeProvider)
    : ICommandHandler<ShipOrderCommand>
{
    private static readonly Uri ShipmentCommands = new($"queue:{CreateShipment.QueueName}");

    public Task<Result> HandleAsync(ShipOrderCommand command, CancellationToken cancellationToken = default)
    {
        var id = new OrderId(command.OrderId);
        return dbContext.Orders
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken)
            .ToResultAsync(OrderErrors.NotFound(command.OrderId))
            .TapAsync(order => order.MarkShipped(timeProvider.GetUtcNow()))
            .BindAsync(async order =>
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                var endpoint = await sendEndpoints.GetSendEndpointAsync(ShipmentCommands, cancellationToken);
                await endpoint.SendAsync(new CreateShipment(order.Id.Value, command.Recipient), cancellationToken);
                return Result.Success();
            });
    }
}
