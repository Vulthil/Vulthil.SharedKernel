using Catalog.Application;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;

namespace Catalog.Endpoints;

public sealed class AddProduct : IEndpoint
{
    public sealed record Request(string Sku, string Name, decimal Price);

    public sealed record Response(Guid Id);

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("products", async (Request request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new AddProductCommand(request.Sku, request.Name, request.Price), cancellationToken);
                return result
                    .Map(id => new Response(id))
                    .ToCreatedAtRouteHttpResult("GetProduct", response => new { id = response.Id });
            })
            .WithName("AddProduct")
            .WithTags("Products")
            .ProducesErrors(ErrorType.Validation, ErrorType.Conflict);
}
