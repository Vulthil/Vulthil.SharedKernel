
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace Vulthil.xUnit.Fixtures;

public abstract class TestContainerFixtureWithConnectionString<TBuilderEntity, TContainerEntity>(IMessageSink messageSink)
    : TestContainerFixture<TBuilderEntity, TContainerEntity>(messageSink), ITestContainerWithConnectionString
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer
{
    public abstract string ConnectionString { get; }
    public abstract string ConnectionStringKey { get; }

    protected override ValueTask InitializeAsync() => base.InitializeAsync();
    protected override ValueTask DisposeAsyncCore() => base.DisposeAsyncCore();
}


public abstract class TestDatabaseContainerFixture<TDbContext, TBuilderEntity, TContainerEntity>(IMessageSink messageSink)
    : DbContainerFixture<TBuilderEntity, TContainerEntity>(messageSink), ITestDatabaseContainer
    where TDbContext : DbContext
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
    private Respawner? _respawner;
    private bool _hasBeenMigrated;

    protected abstract IDbAdapter DbAdapter { get; }
    public abstract string ConnectionStringKey { get; }

    protected override ValueTask InitializeAsync() => base.InitializeAsync();
    protected override ValueTask DisposeAsyncCore() => base.DisposeAsyncCore();

    public async ValueTask MigrateDatabase(IServiceProvider serviceProvider)
    {
        if (_hasBeenMigrated)
        {
            return;
        }

        var dbContext = serviceProvider.GetRequiredService<TDbContext>();
        await dbContext.Database.MigrateAsync();
        _hasBeenMigrated = true;
    }

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
        });
    }

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
