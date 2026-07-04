using Microsoft.Extensions.DependencyInjection;
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

    private void UseTransportProvider(Func<IServiceProvider, ITransport> transportFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITransport>(transportFactory);
        var provider = services.BuildServiceProvider();

        Use<IServiceProvider>(provider);
        Use(provider.GetRequiredService<IServiceProviderIsService>());
    }

    [Fact]
    public async Task SuccessfulStartStartsTransportOnce()
    {
        // Arrange
        var transport = GetMock<ITransport>();
        transport.Setup(t => t.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UseTransportProvider(_ => transport.Object);

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
        UseTransportProvider(_ => transport.Object);

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
        UseTransportProvider(_ => transport.Object);
        await Target.StartAsync(CancellationToken);

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBeOneOf(TaskStatus.RanToCompletion, TaskStatus.Canceled);
    }

    [Fact]
    public async Task TransportResolutionFailureIsRetriedInsteadOfFaultingHostedServiceConstruction()
    {
        // Arrange — the transport factory fails the first time it is resolved (e.g. an unreachable broker
        // connection), succeeding once the retry loop resolves it again.
        var attempts = 0;
        var transport = GetMock<ITransport>();
        transport.Setup(t => t.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UseTransportProvider(_ => ++attempts == 1
            ? throw new InvalidOperationException("broker unreachable")
            : transport.Object);

        // Act — merely constructing the target must not throw, and starting it must reach the retry loop.
        await Target.StartAsync(CancellationToken);
        await Target.ExecuteTask!;

        // Assert
        Target.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
        attempts.ShouldBe(2);
        transport.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
