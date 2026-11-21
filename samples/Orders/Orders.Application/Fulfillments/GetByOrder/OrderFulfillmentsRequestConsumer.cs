using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Orders.Application.Fulfillments.GetByOrder;

public sealed class OrderFulfillmentsRequestConsumer(IOrdersDbContext dbContext) : IRequestConsumer<GetOrderFulfillments, List<FulfillmentDto>>
{
    public Task<List<FulfillmentDto>> ConsumeAsync(IMessageContext<GetOrderFulfillments> messageContext, CancellationToken cancellationToken = default) =>
        dbContext.Fulfillments
            .Where(fulfillment => fulfillment.OrderId == messageContext.Message.OrderId)
            .Select(fulfillment => FulfillmentDto.FromModel(fulfillment))
            .ToListAsync(cancellationToken);
}
