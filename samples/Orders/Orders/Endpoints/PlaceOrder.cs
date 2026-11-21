using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.MainEntities.Create;

namespace WebApi.MainEntity;

public static class Create
{
    public sealed record Request(string Name);

    /// <summary>
    /// The response for the Create endpoint, containing the unique identifier of the newly created MainEntity.
    /// </summary>
    /// <param name="Id">The id of the newly created MainEntity.</param>
    public sealed record Response(Guid Id);

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("main-entities", async (ICommandHandler<CreateMainEntityCommand, Result<Guid>> handler, Request request, CancellationToken cancellationToken) =>
            {
                var command = new CreateMainEntityCommand(request.Name);
                var result = await handler.HandleAsync(command, cancellationToken);
                return result
                    .Map(id => new Response(id))
                    .ToCreatedAtRouteHttpResult("GetMainEntity", r => r);
            });
        }

    }
}
