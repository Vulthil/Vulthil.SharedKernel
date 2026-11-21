using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.Shipping;

namespace WebApi.MainEntity;

public static class Ship
{
    public sealed record Request(string Recipient);

    /// <summary>
    /// The Shipments service creates the shipment later, so a success answers 202 Accepted. <c>Match</c> folds the
    /// result into a response of either shape, and <see cref="CustomResults.Problem"/> builds the same problem response
    /// as <c>ToIResult</c> would for the failure.
    /// </summary>
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("main-entities/{id:guid}/shipments", async (
                Guid id,
                Request request,
                ICommandHandler<ShipMainEntityCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new ShipMainEntityCommand(id, request.Recipient), cancellationToken);
                return result.Match<IResult>(
                    onSuccess: () => TypedResults.Accepted($"/api/main-entities/{id}"),
                    onFailure: CustomResults.Problem);
            })
            .WithName("ShipMainEntity")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesErrors(ErrorType.Validation, ErrorType.NotFound);
        }
    }
}
