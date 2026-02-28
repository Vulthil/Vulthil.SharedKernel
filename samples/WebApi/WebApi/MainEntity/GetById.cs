using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.MainEntities.GetById;

namespace WebApi.MainEntity;

/// <summary>
/// Represents the GetById.
/// </summary>
public static class GetById
{
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
}
