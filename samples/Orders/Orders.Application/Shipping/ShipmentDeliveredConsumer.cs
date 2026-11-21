using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Results;
using WebApi.Domain.SideEffects;

namespace WebApi.Application.Shipping;

/// <summary>
/// Completes the side effect of the shipped entity. A delivery can arrive before the side effect exists (the
/// side-effect consumer runs on another queue), so a missing side effect throws and the delivery is retried.
/// </summary>
public sealed class ShipmentDeliveredConsumer(IWebApiDbContext dbContext, ILogger<ShipmentDeliveredConsumer> logger) : IConsumer<ShipmentDelivered>
{
    public async Task ConsumeAsync(IMessageContext<ShipmentDelivered> messageContext, CancellationToken cancellationToken = default)
    {
        var delivered = messageContext.Message;

        var sideEffects = await dbContext.SideEffects
            .Where(sideEffect => sideEffect.MainEntityId == delivered.OrderReference)
            .ToListAsync(cancellationToken);
        var sideEffect = sideEffects.FirstOrDefault(s => s.Status is Status.InProgressStatus)
            ?? sideEffects.FirstOrDefault()
            ?? throw new InvalidOperationException($"No side effect exists yet for {delivered.OrderReference}.");

        await sideEffect.Complete(delivered.DeliveredAtUtc, delivered.TrackingEventCount)
            .TapError(error => logger.LogInformation("Shipment {ShipmentId} changed nothing: {Error}", delivered.ShipmentId, error.Description))
            .TapAsync(() => dbContext.SaveChangesAsync(cancellationToken));
    }
}
