using System.Data.Common;
using Npgsql;
using Respawn;
using ServiceDefaults;
using Testcontainers.PostgreSql;
using Vulthil.xUnit.Fixtures;
using Orders.Infrastructure.Data;

namespace Orders.Tests.Fixtures;

internal sealed class PostgreSqlTestContainer : TestDatabaseContainerFixture<OrdersDbContext, PostgreSqlBuilder, PostgreSqlContainer>
{
    private readonly PostgreSqlBuilder _builder = new PostgreSqlBuilder("postgres:18.1")
        .WithPassword("webapi");
    protected override PostgreSqlBuilder Configure() => _builder;

    protected override IDbAdapter DbAdapter => Respawn.DbAdapter.Postgres;
    public override DbProviderFactory DbProviderFactory => NpgsqlFactory.Instance;
    public override string ConnectionStringKey => ServiceNames.OrdersDatabaseServiceName;
}
