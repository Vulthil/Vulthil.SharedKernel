using Microsoft.EntityFrameworkCore;
#if NET10_0_OR_GREATER
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
#else
using Aspire.Pomelo.EntityFrameworkCore.MySql;
using Microsoft.Extensions.Hosting;
#endif
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;
using Vulthil.SharedKernel.Infrastructure.Relational;

namespace Vulthil.SharedKernel.Infrastructure.MySql;

/// <summary>
/// Extensions to configure provider-specific behavior for database infrastructure when using MySQL.
/// </summary>
public static class DependencyInjectionExtensions
{

#if NET10_0_OR_GREATER
    /// <summary>
    /// Configures the database infrastructure for MySQL: registers a pooled <typeparamref name="TDbContext"/> for
    /// the named connection string and selects the MySQL outbox store (row-level locking via
    /// <c>FOR UPDATE SKIP LOCKED</c>) unless a custom store was chosen via <c>UseOutboxStore</c>. When outbox
    /// processing is enabled, the commit-time relay trigger is wired as well.
    /// </summary>
    /// <remarks>
    /// On .NET 10 the Pomelo-compatible EF Core provider is registered directly and the MySQL server version is
    /// detected from the connection at startup (<c>ServerVersion.AutoDetect</c>), so the database must be reachable
    /// when the host starts. The underlying registrations are deferred until the configuration chain has executed,
    /// so the order of <c>UseMySql</c>, <c>EnableOutboxProcessing</c>, and <c>UseOutboxStore</c> does not matter.
    /// </remarks>
    /// <typeparam name="TDbContext">The application's <c>DbContext</c>; it must expose the outbox set via <c>ISaveOutboxMessages</c>.</typeparam>
    /// <param name="configurator">The database infrastructure configurator.</param>
    /// <param name="connectionStringKey">The connection-string name resolved from the host's configuration.</param>
    /// <param name="configureSettings">An optional action to configure the EF Core/Pomelo options (e.g. command timeout).</param>
    /// <returns>The configurator for chaining.</returns>
    public static IDatabaseInfrastructureConfigurator<TDbContext> UseMySql<TDbContext>(
        this IDatabaseInfrastructureConfigurator<TDbContext> configurator,
        string connectionStringKey,
        Action<MySqlDbContextOptionsBuilder>? configureSettings = null)
        where TDbContext : DbContext, ISaveOutboxMessages
        => UseMySqlCore(configurator, connectionStringKey, configureSettings);
#else
    /// <summary>
    /// Configures the database infrastructure for MySQL: registers <typeparamref name="TDbContext"/> through the
    /// Aspire Pomelo integration for the named connection string and selects the MySQL outbox store (row-level
    /// locking via <c>FOR UPDATE SKIP LOCKED</c>) unless a custom store was chosen via <c>UseOutboxStore</c>. When
    /// outbox processing is enabled, the commit-time relay trigger is wired as well.
    /// </summary>
    /// <remarks>
    /// On .NET 9 the context is registered via the <c>Aspire.Pomelo.EntityFrameworkCore.MySql</c> client
    /// integration, which resolves the connection string and adds health checks, telemetry, and connection
    /// resiliency. The underlying registrations are deferred until the configuration chain has executed, so the
    /// order of <c>UseMySql</c>, <c>EnableOutboxProcessing</c>, and <c>UseOutboxStore</c> does not matter.
    /// </remarks>
    /// <typeparam name="TDbContext">The application's <c>DbContext</c>; it must expose the outbox set via <c>ISaveOutboxMessages</c>.</typeparam>
    /// <param name="configurator">The database infrastructure configurator.</param>
    /// <param name="connectionStringKey">The connection-string name resolved by the Aspire integration.</param>
    /// <param name="configureSettings">An optional action to configure the Aspire integration settings (health checks, tracing, retries).</param>
    /// <param name="configureDbContextOptions">An optional action to configure the DbContext options.</param>
    /// <returns>The configurator for chaining.</returns>
    public static IDatabaseInfrastructureConfigurator<TDbContext> UseMySql<TDbContext>(
        this IDatabaseInfrastructureConfigurator<TDbContext> configurator,
        string connectionStringKey,
        Action<PomeloEntityFrameworkCoreMySqlSettings>? configureSettings = null,
        Action<DbContextOptionsBuilder>? configureDbContextOptions = null)
        where TDbContext : DbContext, ISaveOutboxMessages
        => UseMySqlCore(configurator, connectionStringKey, configureSettings, configureDbContextOptions);
#endif

    private static IDatabaseInfrastructureConfigurator<TDbContext> UseMySqlCore<TDbContext>(
        this IDatabaseInfrastructureConfigurator<TDbContext> configurator,
        string connectionStringKey,
#if NET10_0_OR_GREATER
        Action<MySqlDbContextOptionsBuilder>? configureSettings = null)
#else
        Action<PomeloEntityFrameworkCoreMySqlSettings>? configureSettings = null,
        Action<DbContextOptionsBuilder>? configureDbContextOptions = null)
#endif
        where TDbContext : DbContext, ISaveOutboxMessages
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.OnConfigured(c =>
        {
            if (c is not DatabaseInfrastructureConfigurator<TDbContext> { OutboxStoreCustomized: true })
            {
                c.UseOutboxStore<MySqlOutboxStore<TDbContext>>();
            }

#if NET10_0_OR_GREATER
            var connectionString = c.HostApplicationBuilder.Configuration.GetConnectionString(connectionStringKey)
                ?? throw new InvalidOperationException($"A connection string named '{connectionStringKey}' was not found.");

            c.HostApplicationBuilder.Services.AddDbContextPool<TDbContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), configureSettings));
#else
            c.HostApplicationBuilder.AddMySqlDbContext<TDbContext>(connectionStringKey, configureSettings, configureDbContextOptions);
#endif

            if (c.OutboxProcessingEnabled)
            {
                c.HostApplicationBuilder.Services.AddRelationalOutboxCommitTrigger();
            }
        });

        return configurator;
    }
}
