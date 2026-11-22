using Orders.Application.Orders.Ship;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Endpoints;

public static class ShipOrder
{
    public sealed record Request(string Recipient);

    /// <summary>
    /// The Shipments service creates the shipment later, so a success answers 202 Accepted. <c>Match</c> folds the
    /// result into a response of either shape, and <see cref="CustomResults.Problem"/> builds the same problem response
    /// as <c>ToIResult</c> would for the failure. Shipping twice throws a domain exception, which the problem-details
    /// handler answers with a 409.
    /// </summary>
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("orders/{id:guid}/shipments", async (
                    Guid id,
                    Request request,
                    ICommandHandler<ShipOrderCommand> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.HandleAsync(new ShipOrderCommand(id, request.Recipient), cancellationToken);
                    return result.Match<IResult>(
                        onSuccess: () => TypedResults.Accepted($"/api/orders/{id}"),
                        onFailure: CustomResults.Problem);
                })
                .WithName("ShipOrder")
                .Produces(StatusCodes.Status202Accepted)
                .ProducesErrors(ErrorType.Validation, ErrorType.NotFound, ErrorType.Conflict);
    }
}
