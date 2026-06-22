using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class ConsumerHostedServiceTests : BaseUnitTestCase
{
    private readonly Lazy<ConsumerHostedService> _lazyTarget;

    private ConsumerHostedService Target => _lazyTarget.Value;

    public ConsumerHostedServiceTests()
    {
        _lazyTarget = new(CreateInstance<ConsumerHostedService>);
        Use<TimeProvider>(TimeProvider.System);
    }

    [Fact]
    public async Task SuccessfulStartStartsTransportOnce()
    {
        // Arrange
        var transport = GetMock<ITransport>();
        transport.Setup(t => t.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Use<IEnumerable<ITransport>>([transport.Object]);

        // Act
        await Target.StartAsync(CancellationToken);
        await Target.ExecuteTask!;

        // Assert
        Target.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
        transport.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransientStartupFailureIsRetriedUntilTransportStarts()
    {
        // Arrange
        var transport = GetMock<ITransport>();
        transport
            .SetupSequence(t => t.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException())
            .Returns(Task.CompletedTask);
        Use<IEnumerable<ITransport>>([transport.Object]);

        // Act
        await Target.StartAsync(CancellationToken);
        await Target.ExecuteTask!;

        // Assert
        Target.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
        transport.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task StoppingDuringStartupRetryCompletesGracefully()
    {
        // Arrange
        var transport = GetMock<ITransport>();
        transport
            .Setup(t => t.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        Use<IEnumerable<ITransport>>([transport.Object]);
        await Target.StartAsync(CancellationToken);

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBeOneOf(TaskStatus.RanToCompletion, TaskStatus.Canceled);
    }
}
