using Aspire.Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure.Npgsql;

/// <summary>
/// Extensions to configure provider-specific behavior for database infrastructure when using PostgreSQL.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Configures the database infrastructure to use the PostgreSQL-optimized outbox strategy.
    /// </summary>
    /// <param name="configurator">The database infrastructure configurator.</param>
    /// <param name="connectionStringKey">The key for the connection string.</param>
    /// <param name="configureSettings">Optional action to configure PostgreSQL-specific settings.</param>
    /// <returns>The configurator for chaining.</returns>
    public static IDatabaseInfrastructureConfigurator UseNpgsql<TContext>(
        this IDatabaseInfrastructureConfigurator configurator,
        string connectionStringKey,
        Action<NpgsqlEntityFrameworkCorePostgreSQLSettings>? configureSettings = null)
        where TContext : DbContext
    {
        configurator.UseOutboxStrategy<NpgsqlOutboxStrategy>();
        configurator.HostApplicationBuilder.AddNpgsqlDbContext<TContext>(connectionStringKey, configureSettings);

        return configurator;
    }
}
