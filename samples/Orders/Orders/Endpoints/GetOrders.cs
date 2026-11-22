using Orders.Application.Orders;
using Orders.Application.Orders.GetAll;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Endpoints;

public static class GetOrders
{
    public sealed record Response(IReadOnlyList<OrderDto> Orders);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("orders", async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.SendAsync(new GetOrdersQuery(), cancellationToken);
                    return result.Map(orders => new Response(orders)).ToIResult();
                })
                .WithName("GetOrders");
    }
}
