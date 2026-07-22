using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Infrastructure.Cosmos.OutboxProcessing;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

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
    /// <param name="connectionStringKey">The name of the Cosmos DB connection.</param>
    /// <param name="configureSettings">An optional action to configure the Cosmos DB settings.</param>
    /// <param name="configureDbContextOptions">An optional action to configure the DbContext options.</param>
    /// <returns>The configurator for chaining.</returns>
    public static IDatabaseInfrastructureConfigurator<TDbContext> UseCosmosDb<TDbContext>(
        this IDatabaseInfrastructureConfigurator<TDbContext> configurator,
        string connectionStringKey,
        Action<Aspire.Microsoft.EntityFrameworkCore.Cosmos.EntityFrameworkCoreCosmosSettings>? configureSettings = null,
        Action<DbContextOptionsBuilder>? configureDbContextOptions = null)
        where TDbContext : DbContext, ISaveOutboxMessages
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.OnConfigured(c =>
        {
            if (c is not DatabaseInfrastructureConfigurator<TDbContext> { OutboxStoreCustomized: true })
            {
                c.UseOutboxStore<CosmosOutboxStore<TDbContext>>();
            }

            c.HostApplicationBuilder.AddCosmosDbContext<TDbContext>(connectionStringKey, configureSettings, configureDbContextOptions);
        });
        return configurator;
    }
}
