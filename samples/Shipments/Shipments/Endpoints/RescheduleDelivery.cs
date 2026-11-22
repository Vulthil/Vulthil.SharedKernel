using Shipments.Application.Shipments;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;

namespace Shipments.Endpoints;

/// <summary>
/// Injects the command handler directly instead of <c>ISender</c>; the resolved handler still runs through the
/// pipeline behaviors, so the validator answers an estimate in the past with a 400.
/// </summary>
public sealed class RescheduleDelivery : IEndpoint
{
    public sealed record Request(DateTimeOffset EstimatedDeliveryUtc);

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("shipments/{id:guid}/estimated-delivery", async (
                Guid id,
                Request request,
                ICommandHandler<RescheduleDeliveryCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new RescheduleDeliveryCommand(id, request.EstimatedDeliveryUtc), cancellationToken);
                return result.ToIResult();
            })
            .WithName("RescheduleDelivery")
            .ProducesErrors(ErrorType.Validation, ErrorType.NotFound, ErrorType.Conflict);
}
