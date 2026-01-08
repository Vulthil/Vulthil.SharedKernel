using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.MainEntities.GetById;

namespace WebApi.MainEntity.Create;

public class Create : IEndpoint
{
    public record Request(string Name);
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("mainentity", async (ISender sender, Request request) =>
        {
            var command = new CreateMainEntityCommand(request.Name);
            var result = await sender.SendAsync(command);
            return result.Match(
                (id) => Results.CreatedAtRoute("GetMainEntity", new { id }),
                CustomResults.Problem);
        });
    }
}

public class Get : IEndpoint
{
    public record Request(string Name);
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("mainentity/{id:guid}", async (ISender sender, Guid id) =>
        {
            var query = new GetMainEntityByIdQuery(id);
            var result = await sender.SendAsync(query);
            return result.Match( 
                Results.Ok,
                e => e.ToIResult());
        })
            .WithName("GetMainEntity");
    }
}
