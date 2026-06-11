using Aspire.Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;
using Vulthil.SharedKernel.Infrastructure.Relational;

namespace Vulthil.SharedKernel.Infrastructure.Npgsql;

/// <summary>
/// Extensions to configure provider-specific behavior for database infrastructure when using PostgreSQL.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Configures the database infrastructure to use the PostgreSQL-optimized outbox strategy and
    /// registers an Aspire-wired <see cref="DbContext"/> for the connection string key.
    /// </summary>
    /// <remarks>
    /// The actual call to <c>AddNpgsqlDbContext</c> is deferred until the full configurator chain
    /// has executed, so the order of <see cref="UseNpgsql{TContext}"/> and
    /// <see cref="IDatabaseInfrastructureConfigurator{TDbContext}.EnableOutboxProcessing"/> is irrelevant.
    /// EF Core retries are left enabled: the outbox processor runs its transactional unit inside the context's
    /// execution strategy (<c>Database.CreateExecutionStrategy().ExecuteAsync</c>), which is compatible with the
    /// retrying execution strategy, so there is no need to force <c>DisableRetry</c>. All settings — including
    /// <c>CommandTimeout</c> — are left to the caller.
    /// </remarks>
    /// <param name="configurator">The database infrastructure configurator.</param>
    /// <param name="connectionStringKey">The key for the connection string.</param>
    /// <param name="configureSettings">Optional action to configure PostgreSQL-specific settings.</param>
    /// <returns>The configurator for chaining.</returns>
    public static IDatabaseInfrastructureConfigurator<TDbContext> UseNpgsql<TDbContext>(
        this IDatabaseInfrastructureConfigurator<TDbContext> configurator,
        string connectionStringKey,
        Action<NpgsqlEntityFrameworkCorePostgreSQLSettings>? configureSettings = null)
        where TDbContext : DbContext, ISaveOutboxMessages
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.UseOutboxStore<NpgsqlOutboxStore<TDbContext>>();

        configurator.OnConfigured(c =>
        {
            c.HostApplicationBuilder.AddNpgsqlDbContext<TDbContext>(connectionStringKey, configureSettings);

            if (c.OutboxProcessingEnabled)
            {
                c.HostApplicationBuilder.Services.AddRelationalOutboxCommitTrigger();
            }
        });

        return configurator;
    }
}
