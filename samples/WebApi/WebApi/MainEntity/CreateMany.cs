using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.MainEntities.CreateMany;

namespace WebApi.MainEntity;

public static class CreateMany
{
    public sealed record Request(IReadOnlyList<string> Names);

    public sealed record Response(IReadOnlyList<Guid> Ids);

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("main-entities/batch", async (Request request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new CreateMainEntitiesCommand(request.Names), cancellationToken);
                return result.Map(ids => new Response(ids)).ToIResult();
            })
            .WithName("CreateMainEntities")
            .ProducesErrors(ErrorType.Validation);
        }
    }
}
