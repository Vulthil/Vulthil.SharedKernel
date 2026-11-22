using Shipments.Application.Shipments;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;

namespace Shipments.Endpoints;

public sealed class GetShipment : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("shipments/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetShipmentQuery(id), cancellationToken);
                return result.ToIResult();
            })
            .WithName("GetShipment")
            .ProducesErrors(ErrorType.NotFound);
}
