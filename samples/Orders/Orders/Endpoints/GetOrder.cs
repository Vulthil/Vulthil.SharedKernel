using Orders.Application.Orders;
using Orders.Application.Orders.GetById;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Endpoints;

public static class GetOrder
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("orders/{id:guid}", async (Guid id, IQueryHandler<GetOrderQuery, Result<OrderDto>> handler, CancellationToken cancellationToken) =>
                {
                    var result = await handler.HandleAsync(new GetOrderQuery(id), cancellationToken);
                    return result.ToIResult();
                })
                .WithName("GetOrder")
                .ProducesErrors(ErrorType.NotFound);
    }
}
