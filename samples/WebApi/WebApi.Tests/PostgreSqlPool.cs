using System.Data.Common;
using DotNet.Testcontainers.Builders;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Vulthil.xUnit.Containers;
using WebApi.Infrastructure.Data;
using WebApi.ServiceDefaults;

namespace WebApi.Tests;

public sealed class PostgreSqlPool : DatabaseContainerWithRespawnerPool<WebApiDbContext, PostgreSqlBuilder, PostgreSqlContainer>
{
    protected override int PoolSize => 2;
    public override string KeyName => ServiceNames.PostgresSqlServerServiceName;

    private readonly PostgreSqlBuilder _postgreSqlBuilder = new PostgreSqlBuilder()
        .WithPassword("webapi");
    protected override IContainerBuilder<PostgreSqlBuilder, PostgreSqlContainer> ContainerBuilder => _postgreSqlBuilder;

    protected override async Task<DbConnection> GetOpenConnectionAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    protected override RespawnerOptions RespawnerOptions =>
        new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
        };

}
