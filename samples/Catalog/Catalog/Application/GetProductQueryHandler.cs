using Catalog.Domain;
using Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Catalog.Application;

internal sealed class GetProductQueryHandler(CatalogDbContext dbContext) : IQueryHandler<GetProductQuery, Result<ProductDto>>
{
    public Task<Result<ProductDto>> HandleAsync(GetProductQuery request, CancellationToken cancellationToken = default)
    {
        var id = new ProductId(request.Id);
        return dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken)
            .ToResultAsync(ProductErrors.NotFound(request.Id))
            .MapAsync(ProductDto.FromModel);
    }
}
