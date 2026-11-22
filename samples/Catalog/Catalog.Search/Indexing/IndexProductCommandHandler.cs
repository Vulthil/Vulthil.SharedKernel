using Microsoft.EntityFrameworkCore;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Catalog.Search.Indexing;

/// <summary>
/// Inserts or updates the search entry. Cosmos DB has no transaction that covers this write and the inbox marker, so
/// the write is an upsert: running it again after a crash between the two leaves the same entry.
/// </summary>
internal sealed class IndexProductCommandHandler(ISearchIndexStore store, TimeProvider timeProvider) : ICommandHandler<IndexProductCommand>
{
    public async Task<Result> HandleAsync(IndexProductCommand request, CancellationToken cancellationToken = default)
    {
        var id = ProductSearchEntry.IdFor(request.ProductId);
        var entry = await store.SearchEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entry is null)
        {
            store.SearchEntries.Add(ProductSearchEntry.Create(request.ProductId, request.Sku, request.Name, request.Price, timeProvider.GetUtcNow()));
        }
        else
        {
            entry.Update(request.Sku, request.Name, request.Price, timeProvider.GetUtcNow());
        }

        await store.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
