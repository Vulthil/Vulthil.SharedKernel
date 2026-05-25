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
            app.MapGet("main-entities/{id:guid}", async (IQueryHandler<GetMainEntityByIdQuery, Result<MainEntityDto>> sender, Guid id) =>
            {
                var query = new GetMainEntityByIdQuery(id);
                var result = await sender.HandleAsync(query);
                return result.ToIResult();
            })
            .WithName("GetMainEntity");
        }
    }
}
