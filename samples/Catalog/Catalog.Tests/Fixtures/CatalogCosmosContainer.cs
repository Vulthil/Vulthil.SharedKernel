using Catalog.Infrastructure;
using ServiceDefaults;
using Vulthil.xUnit.Cosmos;

namespace Catalog.Tests.Fixtures;

/// <summary>
/// The Cosmos DB emulator fixture. It points <see cref="CatalogDbContext"/> at a database per factory scope, creates
/// it from the model, and deletes and recreates it between tests.
/// </summary>
internal sealed class CatalogCosmosContainer : CosmosTestContainerFixture<CatalogDbContext>
{
    public override string ConnectionStringKey => ServiceNames.CatalogDatabaseServiceName;

    protected override string DatabaseName => "catalog";
}
