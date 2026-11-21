using Shipments.Application.Shipments;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;

namespace Shipments.Endpoints;

public sealed class GetShipmentsByOrder : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("shipments", async (Guid orderReference, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetShipmentsByOrderQuery(orderReference), cancellationToken);
                return result.ToIResult();
            })
            .WithName("GetShipmentsByOrder");
}
