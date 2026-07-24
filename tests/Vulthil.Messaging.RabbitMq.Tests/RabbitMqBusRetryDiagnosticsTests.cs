using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.RabbitMq.HealthChecks;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqBusRetryDiagnosticsTests : BaseUnitTestCase
{
    private const int IgnoredExceptionUnresolvableEventId = 1108;

    private readonly CapturingLogger _logger = new();
    private readonly Lazy<RabbitMqBus> _lazyTarget;
    private RabbitMqBus Target => _lazyTarget.Value;

    public RabbitMqBusRetryDiagnosticsTests()
    {
        _lazyTarget = new(CreateInstance<RabbitMqBus>);
        var channel = GetMock<IChannel>();
        channel
            .Setup(c => c.QueueDeclareAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("queue", 0, 0));
        channel
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("consumer-tag");

        GetMock<IConnection>()
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);

        Use(new RabbitMqBusStartupStatus());
        Use<ILoggerFactory>(NullLoggerFactory.Instance);
        Use<ILogger<RabbitMqBus>>(_logger);
    }

    protected override ValueTask Dispose() => _lazyTarget.IsValueCreated ? Target.DisposeAsync() : base.Dispose();

    [Fact]
    public async Task StartupWarnsForEachUnresolvableIgnoredExceptionName()
    {
        // Arrange
        var provider = TestProviders.Build(cfg => cfg.ConfigureQueue("orders", queue =>
        {
            queue.AddConsumer<NoteConsumer>();
            queue.UseRetry(retry => retry.Immediate(2));
            queue.ConfigureQueue(definition =>
            {
                definition.DefaultRetryPolicy!.IgnoreExceptions.Add("Does.Not.Exist.Exception, Nowhere");
                definition.DefaultRetryPolicy.IgnoreExceptions.Add(typeof(InvalidOperationException).AssemblyQualifiedName!);
            });
        }));
        Use(provider);

        // Act
        await Target.StartAsync(CancellationToken);

        // Assert — one warning for the unresolvable name, none for the resolvable one, and the policy still resolves it.
        var warning = _logger.Entries.Where(entry => entry.EventId.Id == IgnoredExceptionUnresolvableEventId).ShouldHaveSingleItem();
        warning.Message.ShouldContain("Does.Not.Exist.Exception, Nowhere");
        warning.Message.ShouldNotContain(nameof(InvalidOperationException));

        var policy = provider.QueueDefinitions.Single().DefaultRetryPolicy!;
        policy.GetIgnoredExceptionTypes().ShouldBe([typeof(InvalidOperationException)]);
    }

    [Fact]
    public async Task StartupDoesNotWarnWhenEveryIgnoredExceptionNameResolves()
    {
        // Arrange
        var provider = TestProviders.Build(cfg => cfg.ConfigureQueue("orders", queue =>
        {
            queue.AddConsumer<NoteConsumer>(consumer => consumer.UseRetry(retry =>
            {
                retry.Immediate(2);
                retry.Ignore<InvalidOperationException>();
            }));
        }));
        Use(provider);

        // Act
        await Target.StartAsync(CancellationToken);

        // Assert
        _logger.Entries.ShouldNotContain(entry => entry.EventId.Id == IgnoredExceptionUnresolvableEventId);
    }

    public sealed record Note(string Text);

    public sealed class NoteConsumer : IConsumer<Note>
    {
        public Task ConsumeAsync(IMessageContext<Note> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class CapturingLogger : ILogger<RabbitMqBus>
    {
        public List<(EventId EventId, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((eventId, formatter(state, exception)));
    }
}
