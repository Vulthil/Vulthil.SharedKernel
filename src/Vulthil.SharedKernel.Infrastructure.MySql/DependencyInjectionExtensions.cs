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
    /// <summary> ... </summary>
    /// <remarks> ... </remarks>
    /// <typeparam name="TContext">...</typeparam>
    /// <param name="configurator">...</param>
    /// <param name="connectionStringKey">...</param>
    /// <param name="configure">... .NET 10 wording ...</param>
    /// <returns>The configurator for chaining.</returns>
    public static IDatabaseInfrastructureConfigurator<TContext> UseMySql<TContext>(
        this IDatabaseInfrastructureConfigurator<TContext> configurator,
        string connectionStringKey,
        Action<MySqlDbContextOptionsBuilder>? configure = null)
        where TContext : DbContext, ISaveOutboxMessages
        => UseMySqlCore(configurator, connectionStringKey, configure);
#else
    /// <summary> ... </summary>
    /// <remarks> ... </remarks>
    /// <typeparam name="TContext">...</typeparam>
    /// <param name="configurator">...</param>
    /// <param name="connectionStringKey">...</param>
    /// <param name="configure">... .NET 9 wording ...</param>
    /// <param name="configureDbContextOptions">An optional action to configure the DbContext options.</param>
    /// <returns>The configurator for chaining.</returns>
    public static IDatabaseInfrastructureConfigurator<TContext> UseMySql<TContext>(
        this IDatabaseInfrastructureConfigurator<TContext> configurator,
        string connectionStringKey,
        Action<PomeloEntityFrameworkCoreMySqlSettings>? configure = null,
        Action<DbContextOptionsBuilder>? configureDbContextOptions = null)
        where TContext : DbContext, ISaveOutboxMessages
        => UseMySqlCore(configurator, connectionStringKey, configure, configureDbContextOptions);
#endif

    private static IDatabaseInfrastructureConfigurator<TContext> UseMySqlCore<TContext>(
        this IDatabaseInfrastructureConfigurator<TContext> configurator,
        string connectionStringKey,
#if NET10_0_OR_GREATER
        Action<MySqlDbContextOptionsBuilder>? configure = null)
#else
        Action<PomeloEntityFrameworkCoreMySqlSettings>? configure = null,
        Action<DbContextOptionsBuilder>? configureDbContextOptions = null)
#endif
        where TContext : DbContext, ISaveOutboxMessages
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.UseOutboxStore<MySqlOutboxStore<TContext>>();

        configurator.OnConfigured(c =>
        {
#if NET10_0_OR_GREATER
            var connectionString = c.HostApplicationBuilder.Configuration.GetConnectionString(connectionStringKey)
                ?? throw new InvalidOperationException($"A connection string named '{connectionStringKey}' was not found.");

            c.HostApplicationBuilder.Services.AddDbContextPool<TContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), configure));
#else
            c.HostApplicationBuilder.AddMySqlDbContext<TContext>(connectionStringKey, configure, configureDbContextOptions);
#endif

            if (c.OutboxProcessingEnabled)
            {
                c.HostApplicationBuilder.Services.AddRelationalOutboxCommitTrigger();
            }
        });

        return configurator;
    }
}
