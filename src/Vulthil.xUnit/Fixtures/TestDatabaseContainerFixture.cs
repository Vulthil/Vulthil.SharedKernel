using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Fixture that wraps a Testcontainers container and exposes a connection string for integration test configuration.
/// </summary>
public abstract class TestContainerFixtureWithConnectionString<TBuilderEntity, TContainerEntity>(IMessageSink messageSink)
    : TestContainerFixture<TBuilderEntity, TContainerEntity>(messageSink), ITestContainerWithConnectionString
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer
{
    /// <summary>
    /// Gets the connection string used to communicate with the containerized service.
    /// </summary>
    public abstract string ConnectionString { get; }
    /// <summary>
    /// Gets the configuration key name where the connection string should be injected.
    /// </summary>
    public abstract string ConnectionStringKey { get; }

    /// <inheritdoc />
    protected override ValueTask InitializeAsync() => base.InitializeAsync();
    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore() => base.DisposeAsyncCore();
}


/// <summary>
/// Fixture that wraps a database container, applies EF Core migrations, and supports Respawn-based data resets between tests.
/// </summary>
public abstract class TestDatabaseContainerFixture<TDbContext, TBuilderEntity, TContainerEntity>(IMessageSink messageSink)
    : DbContainerFixture<TBuilderEntity, TContainerEntity>(messageSink), ITestDatabaseContainer
    where TDbContext : DbContext
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
    private Respawner? _respawner;
    private readonly SemaphoreSlim _migrationLock = new(1);
    private bool _hasBeenMigrated;

    /// <summary>
    /// Gets the Respawn database adapter matching the container's database engine.
    /// </summary>
    protected abstract IDbAdapter DbAdapter { get; }
    /// <summary>
    /// Gets the configuration key name where the connection string should be injected.
    /// </summary>
    public abstract string ConnectionStringKey { get; }

    /// <inheritdoc />
    protected override ValueTask InitializeAsync() => base.InitializeAsync();
    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore()
    {
        _migrationLock.Dispose();
        return base.DisposeAsyncCore();
    }

    /// <inheritdoc />
    public async ValueTask MigrateDatabase(IServiceProvider serviceProvider)
    {
        if (_hasBeenMigrated)
        {
            return;
        }

        var dbContext = serviceProvider.GetRequiredService<TDbContext>();
        var aquiredLock = await _migrationLock.WaitAsync(TimeSpan.FromSeconds(5));
        if (!aquiredLock)
        {
            throw new TimeoutException("Could not acquire migration lock in time.");
        }

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            await dbContext.Database.MigrateAsync();
        }
        _hasBeenMigrated = true;
        _migrationLock.Release();
    }

    /// <inheritdoc />
    public async ValueTask InitializeRespawner()
    {
        if (_respawner is not null)
        {
            return;
        }

        var connection = await OpenConnectionAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter,
            WithReseed = true,
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }

    /// <inheritdoc />
    public async ValueTask ResetDatabase()
    {
        if (_respawner is null)
        {
            return;
        }

        var connection = await OpenConnectionAsync();
        await _respawner.ResetAsync(connection);
    }
}
