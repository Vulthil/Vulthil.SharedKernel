using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure.MySql;

/// <summary>
/// Extensions to configure provider-specific behavior for database infrastructure when using MySQL.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Configures the database infrastructure to use the MySQL-optimized outbox strategy.
    /// </summary>
    /// <param name="configurator">The database infrastructure configurator.</param>
    /// <returns>The configurator for chaining.</returns>
    public static IDatabaseInfrastructureConfigurator<TContext> UseMySql<TContext>(
        this IDatabaseInfrastructureConfigurator<TContext> configurator)
        where TContext : DbContext
    {
        configurator.UseOutboxStrategy<MySqlOutboxStrategy>();
        return configurator;
    }
}
