using Microsoft.EntityFrameworkCore;
using Orders.Application.Fulfillments;
using Orders.Application.Fulfillments.GetByOrder;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Application.Orders.GetAll;

public sealed record GetOrdersQuery : IQuery<Result<IReadOnlyList<OrderDto>>>;

/// <summary>
/// Asks for the fulfillments of every order at once. A failed request does not hide its order: <c>Recover</c>
/// replaces the failure with an empty list, and <c>Combine</c> folds the per-order results into one list.
/// </summary>
public sealed class GetOrdersQueryHandler(IOrdersDbContext dbContext, IRequester requester) : IQueryHandler<GetOrdersQuery, Result<IReadOnlyList<OrderDto>>>
{
    public async Task<Result<IReadOnlyList<OrderDto>>> HandleAsync(GetOrdersQuery query, CancellationToken cancellationToken = default)
    {
        var orders = await dbContext.Orders.AsNoTracking().OrderBy(order => order.Id).ToListAsync(cancellationToken);

        var results = await Task.WhenAll(orders.Select(order =>
            requester.RequestAsync<GetOrderFulfillments, List<FulfillmentDto>>(new GetOrderFulfillments(order.Id.Value), null, cancellationToken)
                .MapAsync(fulfillments => OrderDto.FromModel(order, fulfillments))));

        return results
            .Select((result, index) => result.Recover(_ => OrderDto.FromModel(orders[index], [])))
            .Combine();
    }
}
