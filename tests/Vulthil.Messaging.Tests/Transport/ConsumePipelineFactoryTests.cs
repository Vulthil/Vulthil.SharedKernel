using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

public sealed class ConsumePipelineFactoryTests : BaseUnitTestCase
{
    private sealed record TestMessage(string Value);

    private sealed class RecordingFilter(string name, List<string> log, bool shortCircuit = false) : IConsumeFilter<TestMessage>
    {
        public async Task ConsumeAsync(IMessageContext<TestMessage> context, ConsumeDelegate<TestMessage> next)
        {
            log.Add($"{name}:before");
            if (!shortCircuit)
            {
                await next(context);
            }
            log.Add($"{name}:after");
        }
    }

    private static MessageContext<TestMessage> CreateContext() => new()
    {
        Message = new TestMessage("x"),
        CorrelationId = "c",
        RoutingKey = "r",
        Headers = new Dictionary<string, object?>(),
    };

    [Fact]
    public async Task BuildWithoutFiltersReturnsTerminalUnwrapped()
    {
        // Arrange
        var log = new List<string>();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        ConsumeDelegate<TestMessage> terminal = _ =>
        {
            log.Add("terminal");
            return Task.CompletedTask;
        };

        // Act
        var pipeline = ConsumePipelineFactory.Build(serviceProvider, terminal);
        await pipeline(CreateContext());

        // Assert
        log.ShouldBe(["terminal"]);
    }

    [Fact]
    public async Task BuildComposesFiltersFirstRegisteredOutermost()
    {
        // Arrange
        var log = new List<string>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IConsumeFilter<TestMessage>>(new RecordingFilter("outer", log))
            .AddSingleton<IConsumeFilter<TestMessage>>(new RecordingFilter("inner", log))
            .BuildServiceProvider();
        ConsumeDelegate<TestMessage> terminal = _ =>
        {
            log.Add("terminal");
            return Task.CompletedTask;
        };

        // Act
        var pipeline = ConsumePipelineFactory.Build(serviceProvider, terminal);
        await pipeline(CreateContext());

        // Assert
        log.ShouldBe(["outer:before", "inner:before", "terminal", "inner:after", "outer:after"]);
    }

    [Fact]
    public async Task BuildShortCircuitsWhenFilterSkipsNext()
    {
        // Arrange
        var log = new List<string>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IConsumeFilter<TestMessage>>(new RecordingFilter("gate", log, shortCircuit: true))
            .BuildServiceProvider();
        ConsumeDelegate<TestMessage> terminal = _ =>
        {
            log.Add("terminal");
            return Task.CompletedTask;
        };

        // Act
        var pipeline = ConsumePipelineFactory.Build(serviceProvider, terminal);
        await pipeline(CreateContext());

        // Assert
        log.ShouldBe(["gate:before", "gate:after"]);
    }
}
