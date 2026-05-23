using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Vulthil.SharedKernel.Infrastructure.Relational;

/// <summary>
/// Provides extension methods for applying pending Entity Framework Core migrations to a specified DbContext using
/// dependency injection.
/// </summary>
/// <remarks>These methods are intended to be used during application startup to ensure that the database schema
/// is up to date with the current model. They resolve the specified DbContext from the dependency injection container
/// and apply any pending migrations. Use these methods to automate database updates in hosted applications.</remarks>
public static class DependencyInjection
{
    /// <summary>
    /// Applies any pending EF Core migrations for <typeparamref name="TDbContext"/>.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type.</typeparam>
    /// <param name="host">The host whose services to use.</param>
    public static Task MigrateAsync<TDbContext>(this IHost host)
        where TDbContext : DbContext
        => host.Services.MigrateAsync<TDbContext>();

    /// <summary>
    /// Applies any pending EF Core migrations for <typeparamref name="TDbContext"/> using the provided service provider.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type.</typeparam>
    /// <param name="services">The service provider to resolve the context from.</param>
    public static async Task MigrateAsync<TDbContext>(this IServiceProvider services)
        where TDbContext : DbContext
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            await context.Database.MigrateAsync();
        }
    }
}
