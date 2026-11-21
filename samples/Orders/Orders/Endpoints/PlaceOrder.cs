using Orders.Application.Orders.Place;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Endpoints;

public static class PlaceOrder
{
    public sealed record Request(string Description);

    public sealed record Response(Guid Id);

    /// <summary>
    /// Injects the command handler directly; it still runs through the validation and transaction behaviors.
    /// </summary>
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("orders", async (Request request, ICommandHandler<PlaceOrderCommand, Result<Guid>> handler, CancellationToken cancellationToken) =>
                {
                    var result = await handler.HandleAsync(new PlaceOrderCommand(request.Description), cancellationToken);
                    return result
                        .Map(id => new Response(id))
                        .ToCreatedAtRouteHttpResult("GetOrder", response => new { id = response.Id });
                })
                .WithName("PlaceOrder")
                .ProducesErrors(ErrorType.Validation);
    }
}
