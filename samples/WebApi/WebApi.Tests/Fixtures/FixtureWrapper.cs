using Vulthil.xUnit.Fixtures;
using Xunit.Sdk;

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Represents the FixtureWrapper.
/// </summary>
public sealed class FixtureWrapper : TestFixture
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    public FixtureWrapper(IMessageSink messageSink) : base()
    {
        AddContainer(new PostgreSqlTestContainer(messageSink));
        AddContainer(new RabbitMqTestContainer(messageSink));
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected override Task ConfigureContainers()
    {
        return Task.CompletedTask;
    }
}
