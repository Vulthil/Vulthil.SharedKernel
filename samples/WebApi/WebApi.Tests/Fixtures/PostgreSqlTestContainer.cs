using System.Data.Common;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Vulthil.xUnit.Fixtures;
using WebApi.Infrastructure.Data;
using WebApi.ServiceDefaults;
using Xunit.Sdk;

namespace WebApi.Tests.Fixtures;
internal sealed class PostgreSqlTestContainer(IMessageSink messageSink) : TestDatabaseContainerFixture<WebApiDbContext, PostgreSqlBuilder, PostgreSqlContainer>(messageSink)
{
    protected override IDbAdapter DbAdapter => Respawn.DbAdapter.Postgres;
    public override DbProviderFactory DbProviderFactory => NpgsqlFactory.Instance;
    public override string ConnectionStringKey => ServiceNames.PostgresSqlServerServiceName;

    protected override PostgreSqlBuilder Configure(PostgreSqlBuilder builder) => builder
        .WithPassword("webapi");
}

public sealed class RabbitMqTestContainer(IMessageSink messageSink) : TestContainerFixtureWithConnectionString<RabbitMqBuilder, RabbitMqContainer>(messageSink)
{
    protected override RabbitMqBuilder Configure(RabbitMqBuilder builder) => builder
        .WithUsername("guest")
        .WithPassword("guest");

    public override string ConnectionStringKey => ServiceNames.RabbitMqServiceName;
    public override string ConnectionString => Container.GetConnectionString();

}
