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
    /// <typeparam name="TDbContext">...</typeparam>
    /// <param name="configurator">...</param>
    /// <param name="connectionStringKey">...</param>
    /// <param name="configureSettings">... .NET 10 wording ...</param>
    /// <returns>The configurator for chaining.</returns>
    public static IDatabaseInfrastructureConfigurator<TDbContext> UseMySql<TDbContext>(
        this IDatabaseInfrastructureConfigurator<TDbContext> configurator,
        string connectionStringKey,
        Action<MySqlDbContextOptionsBuilder>? configureSettings = null)
        where TDbContext : DbContext, ISaveOutboxMessages
        => UseMySqlCore(configurator, connectionStringKey, configureSettings);
#else
    /// <summary> ... </summary>
    /// <remarks> ... </remarks>
    /// <typeparam name="TDbContext">...</typeparam>
    /// <param name="configurator">...</param>
    /// <param name="connectionStringKey">...</param>
    /// <param name="configureSettings">... .NET 9 wording ...</param>
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

        configurator.UseOutboxStore<MySqlOutboxStore<TDbContext>>();

        configurator.OnConfigured(c =>
        {
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
