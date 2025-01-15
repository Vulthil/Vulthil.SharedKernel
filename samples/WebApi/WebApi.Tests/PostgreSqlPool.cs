using System.Data.Common;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Vulthil.SharedKernel.xUnit.Containers;
using WebApi.Data;

namespace WebApi.Tests;

public sealed class PostgreSqlPool : DatabaseContainerPool<WebApiDbContext, PostgreSqlBuilder, PostgreSqlContainer>
{
    protected override int PoolSize => 2;
    private readonly PostgreSqlBuilder _postgreSqlBuilder = new PostgreSqlBuilder()
        .WithPassword("webapi");

    protected override IContainerBuilder<PostgreSqlBuilder, PostgreSqlContainer> ContainerBuilder => _postgreSqlBuilder;

    protected override (RespawnerOptions RespawnerOptions, Func<string, Task<DbConnection>> ConnectionFactory) RespawnerOptions => (
        new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
        },
        async connectionString =>
        {
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }
    );

    protected override Func<string, Action<DbContextOptionsBuilder>> OptionsAction => connectionString =>
    options =>
        options.UseNpgsql(connectionString);

    public override async Task ApplyMigrations(IServiceProvider services, IDatabaseContainer container)
    {
        if (container.HasBeenMigrated)
        {
            return;
        }

        await using var dbContext = services.GetRequiredService<WebApiDbContext>();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            await dbContext.Database.MigrateAsync();
        }

        container.MarkMigrated();
    }
}
