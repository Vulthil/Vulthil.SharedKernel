using Microsoft.EntityFrameworkCore;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Orders.Domain.Fulfillments;

namespace Orders.Application.Fulfillments.GetById;

public sealed record GetFulfillmentQuery(Guid Id) : IQuery<Result<FulfillmentDto>>;

internal sealed class GetFulfillmentQueryHandler(IOrdersDbContext dbContext) : IQueryHandler<GetFulfillmentQuery, Result<FulfillmentDto>>
{
    public Task<Result<FulfillmentDto>> HandleAsync(GetFulfillmentQuery request, CancellationToken cancellationToken = default)
    {
        var id = new FulfillmentId(request.Id);
        return dbContext.Fulfillments
            .AsNoTracking()
            .FirstOrDefaultAsync(fulfillment => fulfillment.Id == id, cancellationToken)
            .ToResultAsync(FulfillmentErrors.NotFound(request.Id))
            .MapAsync(FulfillmentDto.FromModel);
    }
}
