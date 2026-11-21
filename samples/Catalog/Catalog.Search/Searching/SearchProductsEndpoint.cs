using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;

namespace Catalog.Search.Searching;

public sealed class SearchProductsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("search/products", async (string? term, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new SearchProductsQuery(term ?? string.Empty), cancellationToken);
                return result.ToIResult();
            })
            .WithName("SearchProducts")
            .WithTags("Search")
            .ProducesErrors(ErrorType.Validation);
}
