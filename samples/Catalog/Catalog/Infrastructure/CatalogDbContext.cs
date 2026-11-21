using System.Reflection;
using Catalog.Domain;
using Catalog.Search;
using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Inbox.Cosmos;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.Cosmos;
using Vulthil.SharedKernel.Infrastructure.Data;

namespace Catalog.Infrastructure;

/// <summary>
/// The Cosmos DB context of the Catalog service. It holds the products, the search module's entries, the outbox and
/// the inbox markers, each in a container of its own.
/// </summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : BaseDbContext(options), ISaveInboxMessages, ISearchIndexStore
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductSearchEntry> SearchEntries => Set<ProductSearchEntry>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override Assembly? ConfigurationAssembly => typeof(CatalogDbContext).Assembly;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SearchModule).Assembly);
        modelBuilder.ApplyCosmosOutbox();
        modelBuilder.ApplyCosmosInbox();
    }
}
