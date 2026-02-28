using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.MainEntities.Create;

namespace WebApi.MainEntity;

/// <summary>
/// Represents the Create.
/// </summary>
public static class Create
{
    /// <summary>
    /// Represents the Request.
    /// </summary>
    public record Request(string Name);
    /// <summary>
    /// Represents the Endpoint.
    /// </summary>
    public class Endpoint : IEndpoint
    {
        /// <summary>
        /// Executes this member.
        /// </summary>
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
