using System.Data.Common;
using Npgsql;
using Respawn;
using ServiceDefaults;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Vulthil.xUnit.Fixtures;
using WebApi.Infrastructure.Data;
using Xunit.Sdk;

namespace WebApi.Tests.Fixtures;

internal sealed class PostgreSqlTestContainer(IMessageSink messageSink) : TestDatabaseContainerFixture<WebApiDbContext, PostgreSqlBuilder, PostgreSqlContainer>(messageSink)
{
    private readonly PostgreSqlBuilder _builder = new PostgreSqlBuilder("postgres:18.1")
        .WithPassword("webapi");
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected override PostgreSqlBuilder Configure() => _builder;

    /// <summary>
    /// Represents this member.
    /// </summary>
    protected override IDbAdapter DbAdapter => Respawn.DbAdapter.Postgres;
    /// <summary>
    /// Represents this member.
    /// </summary>
    public override DbProviderFactory DbProviderFactory => NpgsqlFactory.Instance;
    /// <summary>
    /// Represents this member.
    /// </summary>
    public override string ConnectionStringKey => ServiceNames.PostgresSqlServerServiceName;
}


/// <summary>
/// Represents the RabbitMqTestContainer.
/// </summary>
public sealed class RabbitMqTestContainer(IMessageSink messageSink) : TestContainerFixtureWithConnectionString<RabbitMqBuilder, RabbitMqContainer>(messageSink)
{
    private readonly RabbitMqBuilder _builder = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername("guest")
        .WithPassword("guest");

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected override RabbitMqBuilder Configure() => _builder;

    /// <summary>
    /// Represents this member.
    /// </summary>
    public override string ConnectionStringKey => ServiceNames.RabbitMqServiceName;
    /// <summary>
    /// Executes this member.
    /// </summary>
    public override string ConnectionString => Container.GetConnectionString();
}
