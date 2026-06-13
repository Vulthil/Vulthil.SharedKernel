using System.Data.Common;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Vulthil.TestHost;
using Vulthil.TestHost.Data;
using Vulthil.xUnit.Fixtures;
using Xunit.Sdk;

namespace Vulthil.IntegrationTests.Fixtures;

internal sealed class PostgreSqlTestContainer(IMessageSink messageSink) : TestDatabaseContainerFixture<TestHostDbContext, PostgreSqlBuilder, PostgreSqlContainer>(messageSink)
{
    private readonly PostgreSqlBuilder _builder = new PostgreSqlBuilder("postgres:18.1")
        .WithPassword("vulthil");
    protected override PostgreSqlBuilder Configure() => _builder;

    protected override IDbAdapter DbAdapter => Respawn.DbAdapter.Postgres;
    public override DbProviderFactory DbProviderFactory => NpgsqlFactory.Instance;
    public override string ConnectionStringKey => TestHostConnectionStrings.Postgres;
}
