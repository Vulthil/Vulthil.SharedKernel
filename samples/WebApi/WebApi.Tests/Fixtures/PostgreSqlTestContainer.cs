using System.Data.Common;
using Npgsql;
using Respawn;
using ServiceDefaults;
using Testcontainers.PostgreSql;
using Vulthil.xUnit.Fixtures;
using WebApi.Infrastructure.Data;
using Xunit.Sdk;

namespace WebApi.Tests.Fixtures;

internal sealed class PostgreSqlTestContainer(IMessageSink messageSink) : TestDatabaseContainerFixture<WebApiDbContext, PostgreSqlBuilder, PostgreSqlContainer>(messageSink)
{
    private readonly PostgreSqlBuilder _builder = new PostgreSqlBuilder("postgres:18.1")
        .WithPassword("webapi");
    protected override PostgreSqlBuilder Configure() => _builder;

    protected override IDbAdapter DbAdapter => Respawn.DbAdapter.Postgres;
    public override DbProviderFactory DbProviderFactory => NpgsqlFactory.Instance;
    public override string ConnectionStringKey => ServiceNames.PostgresSqlServerServiceName;
}
