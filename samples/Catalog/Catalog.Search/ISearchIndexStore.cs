using Microsoft.EntityFrameworkCore;

namespace Catalog.Search;

/// <summary>
/// What the search module needs from the host's context. The module never sees the host's <c>DbContext</c> type;
/// the host implements this interface and registers it.
/// </summary>
public interface ISearchIndexStore
{
    DbSet<ProductSearchEntry> SearchEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
