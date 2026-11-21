using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;

namespace WebApi.Application.Shipping;

/// <summary>
/// A consume filter for one message type. A delivery that can never be processed is skipped by not calling
/// <c>next</c>: the consumer does not run, and the delivery is acknowledged instead of retried.
/// </summary>
public sealed class ShipmentDeliveredValidationFilter(TimeProvider timeProvider, ILogger<ShipmentDeliveredValidationFilter> logger)
    : IConsumeFilter<ShipmentDelivered>
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public Task ConsumeAsync(IMessageContext<ShipmentDelivered> context, ConsumeDelegate<ShipmentDelivered> next)
    {
        var delivered = context.Message;
        if (delivered.OrderReference == Guid.Empty || delivered.DeliveredAtUtc > timeProvider.GetUtcNow().Add(ClockSkew))
        {
            logger.LogWarning("Skipping invalid ShipmentDelivered {MessageId} for shipment {ShipmentId}", context.MessageId, delivered.ShipmentId);
            return Task.CompletedTask;
        }

        return next(context);
    }
}
