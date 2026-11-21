using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.MainEntities.GetById;

namespace WebApi.MainEntity;

public static class GetAll
{

    public sealed record Response(IReadOnlyList<MainEntityDto> MainEntities);

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("main-entities", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var query = new GetMainEntities();
                var result = await sender.SendAsync(query, cancellationToken);
                return result.Map(r => new Response(r)).ToIResult();
            })
            .WithName("GetMainEntities");
        }

    }
}
