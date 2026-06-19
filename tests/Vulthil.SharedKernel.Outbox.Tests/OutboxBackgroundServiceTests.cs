using Microsoft.Extensions.Options;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

public sealed class OutboxBackgroundServiceTests : BaseUnitTestCase
{
    private readonly Lazy<OutboxBackgroundService> _lazyTarget;

    private OutboxBackgroundService Target => _lazyTarget.Value;

    public OutboxBackgroundServiceTests()
    {
        _lazyTarget = new(CreateInstance<OutboxBackgroundService>);
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions()));
    }

    [Fact]
    public async Task StoppingWhileWaitingForRelayGatesCompletesGracefully()
    {
        // Arrange
        var gate = new BlockingRelayGate();
        Use<IEnumerable<IOutboxRelayGate>>([gate]);
        await Target.StartAsync(CancellationToken);
        await gate.Entered;

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    private sealed class BlockingRelayGate : IOutboxRelayGate
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public Task WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }
}
