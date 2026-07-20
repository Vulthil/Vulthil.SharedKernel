using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Tests.Testing;

public sealed class BaseUnitTestCaseDisposalTests : BaseUnitTestCase<BaseUnitTestCaseDisposalTests.DisposableTarget>
{
    private bool _created;

    protected override DisposableTarget CreateInstance()
    {
        _created = true;
        return base.CreateInstance();
    }

    [Fact]
    public async Task DisposingAfterAccessingTargetDisposesItExactlyOnce()
    {
        // Arrange
        var target = Target;

        // Act
        await DisposeAsync();

        // Assert
        target.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task DisposingWithoutEverAccessingTargetNeverCreatesIt()
    {
        // Act
        await DisposeAsync();

        // Assert
        _created.ShouldBeFalse();
    }

    public sealed class DisposableTarget : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}

public sealed class BaseUnitTestCaseAsyncDisposableTargetTests : BaseUnitTestCase<BaseUnitTestCaseAsyncDisposableTargetTests.AsyncDisposableTarget>
{
    [Fact]
    public async Task DisposingAfterAccessingTargetPrefersAsynchronousDisposal()
    {
        // Arrange
        var target = Target;

        // Act
        await DisposeAsync();

        // Assert
        target.DisposeCount.ShouldBe(1);
    }

    public sealed class AsyncDisposableTarget : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
