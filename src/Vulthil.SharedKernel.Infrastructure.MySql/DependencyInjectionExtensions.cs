using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;
using Vulthil.SharedKernel.Infrastructure.Relational;

namespace Vulthil.SharedKernel.Infrastructure.MySql;

/// <summary>
/// Extensions to configure provider-specific behavior for database infrastructure when using MySQL.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Configures the database infrastructure for MySQL: registers a pooled <typeparamref name="TDbContext"/> for
    /// the named connection string and selects the MySQL outbox store (row-level locking via
    /// <c>FOR UPDATE SKIP LOCKED</c>) unless a custom store was chosen via <c>UseOutboxStore</c>. When outbox
    /// processing is enabled, the commit-time relay trigger is wired as well.
    /// </summary>
    /// <remarks>
    /// The Pomelo-compatible EF Core provider is registered directly and the MySQL server version is detected from
    /// the connection at startup (<c>ServerVersion.AutoDetect</c>), so the database must be reachable when the host
    /// starts. The underlying registrations are deferred until the configuration chain has executed, so the order of
    /// <c>UseMySql</c>, <c>EnableOutboxProcessing</c>, and <c>UseOutboxStore</c> does not matter.
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
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.UseDefaultOutboxStore<MySqlOutboxStore<TDbContext>>();

        configurator.OnConfigured(c =>
        {
            var connectionString = c.HostApplicationBuilder.Configuration.GetConnectionString(connectionStringKey)
                ?? throw new InvalidOperationException($"A connection string named '{connectionStringKey}' was not found.");

            c.HostApplicationBuilder.Services.AddDbContextPool<TDbContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), configureSettings));

            if (c.OutboxProcessingEnabled)
            {
                c.HostApplicationBuilder.Services.AddRelationalOutboxCommitTrigger();
            }
        });

        return configurator;
    }
}
