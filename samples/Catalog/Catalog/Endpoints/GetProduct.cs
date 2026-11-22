using Catalog.Application;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;

namespace Catalog.Endpoints;

public sealed class GetProduct : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("products/{id:guid}", async (Guid id, IQueryHandler<GetProductQuery, Result<ProductDto>> handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new GetProductQuery(id), cancellationToken);
                return result.ToIResult();
            })
            .WithName("GetProduct")
            .WithTags("Products")
            .ProducesErrors(ErrorType.NotFound);
}
