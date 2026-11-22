using Microsoft.EntityFrameworkCore;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Application.Fulfillments.GetInProgress;

public sealed record GetInProgressFulfillmentsQuery : IQuery<Result<List<FulfillmentDto>>>;

internal class GetInProgressFulfillmentsQueryHandler(IOrdersDbContext ordersDbContext) : IQueryHandler<GetInProgressFulfillmentsQuery, Result<List<FulfillmentDto>>>
{
    private readonly IOrdersDbContext _ordersDbContext = ordersDbContext;

    public async Task<Result<List<FulfillmentDto>>> HandleAsync(GetInProgressFulfillmentsQuery request, CancellationToken cancellationToken = default)
    {
        // The status is stored as text with a one-letter prefix (see FulfillmentConfiguration), so SQL finds an
        // in-progress fulfillment by that prefix.
        var list = await _ordersDbContext.Fulfillments
            .Where(s => EF.Functions.Like((string)(object)s.Status, "I %"))
            .ToListAsync(cancellationToken);

        return list
            .Select(FulfillmentDto.FromModel)
            .ToList()
            .ToResult(Error.None);
    }
}
