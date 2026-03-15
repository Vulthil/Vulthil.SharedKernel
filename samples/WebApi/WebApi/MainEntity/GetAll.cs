using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.MainEntities.GetById;

namespace WebApi.MainEntity;

/// <summary>
/// Represents the GetAll.
/// </summary>
public static class GetAll
{

    public sealed record Response(IReadOnlyList<MainEntityDto> MainEntities);

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
            app.MapGet("mainentity", async (ISender sender) =>
            {
                var query = new GetMainEntities();
                var result = await sender.SendAsync(query);
                return result.Map(r => new Response(r)).ToIResult();
            })
            .WithName("GetMainEntities");
        }

    }
}
