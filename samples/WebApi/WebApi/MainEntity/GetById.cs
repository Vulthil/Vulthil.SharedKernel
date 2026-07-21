using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.MainEntities.GetById;

namespace WebApi.MainEntity;

public static class GetById
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("main-entities/{id:guid}", async (IQueryHandler<GetMainEntityByIdQuery, Result<MainEntityDto>> sender, Guid id, CancellationToken cancellationToken) =>
            {
                var query = new GetMainEntityByIdQuery(id);
                var result = await sender.HandleAsync(query, cancellationToken);
                return result.ToIResult();
            })
            .WithName("GetMainEntity");
        }
    }
}
