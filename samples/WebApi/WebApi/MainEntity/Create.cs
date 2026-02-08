using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.MainEntities.Create;

namespace WebApi.MainEntity;

public static class Create
{
    public record Request(string Name);
    public class Endpoint : IEndpoint
    {
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
}
