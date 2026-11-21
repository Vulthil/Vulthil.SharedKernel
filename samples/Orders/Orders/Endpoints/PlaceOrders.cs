using Orders.Application.Orders.PlaceMany;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Endpoints;

public static class PlaceOrders
{
    public sealed record Request(IReadOnlyList<string> Descriptions);

    public sealed record Response(IReadOnlyList<Guid> Ids);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("orders/batch", async (Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.SendAsync(new PlaceOrdersCommand(request.Descriptions), cancellationToken);
                    return result.Map(ids => new Response(ids)).ToIResult();
                })
                .WithName("PlaceOrders")
                .ProducesErrors(ErrorType.Validation);
    }
}
