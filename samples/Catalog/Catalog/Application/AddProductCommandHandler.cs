using Catalog.Domain;
using Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Catalog.Application;

internal sealed class AddProductCommandHandler(CatalogDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<AddProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(AddProductCommand request, CancellationToken cancellationToken = default)
    {
        // AnyAsync translates to an EXISTS subquery, which the Linux Cosmos DB emulator does not run.
        var takenBy = await dbContext.Products
            .Where(product => product.Sku == request.Sku)
            .Select(product => product.Sku)
            .FirstOrDefaultAsync(cancellationToken);
        var skuTaken = takenBy is not null;

        return await Result.Success(request)
            .Ensure(_ => !skuTaken, ProductErrors.SkuTaken(request.Sku))
            .Bind(command => Product.Create(command.Sku, command.Name, command.Price, timeProvider.GetUtcNow()))
            .Tap(product => dbContext.Products.Add(product))
            .TapAsync(_ => dbContext.SaveChangesAsync(cancellationToken))
            .MapAsync(product => product.Id.Value);
    }
}
