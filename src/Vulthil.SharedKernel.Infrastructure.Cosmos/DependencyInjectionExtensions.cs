using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Infrastructure.Cosmos.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure.Cosmos;

/// <summary>
/// Extensions to configure provider-specific behavior for database infrastructure when using Cosmos DB.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Configures the database infrastructure to use the Cosmos-specific outbox strategy.
    /// </summary>
    /// <param name="configurator">The database infrastructure configurator.</param>
    /// <param name="connectionName">The name of the Cosmos DB connection.</param>
    /// <param name="configureSettings">An action to configure the Cosmos DB settings.</param>
    /// <returns>The configurator for chaining.</returns>
    public static IDatabaseInfrastructureConfigurator UseCosmosDb<TDbContext>(
        this IDatabaseInfrastructureConfigurator configurator,
        string connectionName,
        Action<Aspire.Microsoft.EntityFrameworkCore.Cosmos.EntityFrameworkCoreCosmosSettings>? configureSettings)
        where TDbContext : DbContext
    {
        configurator.HostApplicationBuilder.AddCosmosDbContext<TDbContext>(connectionName, configureSettings);
        return configurator.UseOutboxStrategy<CosmosOutboxStrategy>();
    }
}
