using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Application.Fulfillments;
using Orders.Application.Fulfillments.GetByOrder;
using Orders.Domain.Orders;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Application.Orders.GetById;

public sealed record GetOrderQuery(Guid OrderId) : IQuery<Result<OrderDto>>;

/// <summary>
/// Loads the order and asks for its fulfillments at the same time, then joins the two results with <c>Zip</c>: the
/// query fails with the first failure of the two. A fulfillment request that times out is not a failure of the
/// query: <c>OrElse</c> turns that one error into an empty list.
/// </summary>
public sealed class GetOrderQueryHandler(ILogger<GetOrderQueryHandler> logger, IOrdersDbContext dbContext, IRequester requester) : IQueryHandler<GetOrderQuery, Result<OrderDto>>
{
    private static readonly TimeSpan FulfillmentsTimeout = TimeSpan.FromSeconds(3);

    public async Task<Result<OrderDto>> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken = default)
    {
        var id = new OrderId(query.OrderId);
        var orderTask = dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken)
            .ToResultAsync(OrderErrors.NotFound(query.OrderId));
        var fulfillmentsTask = GetFulfillmentsAsync(query.OrderId, cancellationToken);

        var order = await orderTask;
        var fulfillments = await fulfillmentsTask;

        return order.Zip(fulfillments, (o, f) => OrderDto.FromModel(o, f));
    }

    private async Task<Result<List<FulfillmentDto>>> GetFulfillmentsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var fulfillments = await requester.RequestAsync<GetOrderFulfillments, List<FulfillmentDto>>(
            new GetOrderFulfillments(orderId),
            context =>
            {
                context.SetTimeout(FulfillmentsTimeout);
                return ValueTask.CompletedTask;
            },
            cancellationToken);

        return fulfillments
            .TapError(error => logger.LogWarning("The fulfillments of order {OrderId} are unavailable: {Error}", orderId, error.Code))
            .OrElse(error => error.Code == RequestErrorCodes.Timeout
                ? Result.Success(new List<FulfillmentDto>())
                : Result.Failure<List<FulfillmentDto>>(error));
    }
}
