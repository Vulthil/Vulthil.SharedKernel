using Vulthil.Results;
using Vulthil.SharedKernel.Application.Behaviors;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Application.Tests.Pipeline;

public sealed class TransactionalPipelineBehaviorTests : BaseUnitTestCase
{
    private readonly Lazy<TransactionalPipelineBehavior<TestTransactionalCommand, Result>> _lazyTarget;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private Func<Result, bool>? _capturedShouldCommit;
    private TransactionalPipelineBehavior<TestTransactionalCommand, Result> Target => _lazyTarget.Value;

    public TransactionalPipelineBehaviorTests()
    {
        _unitOfWorkMock = GetMock<IUnitOfWork>();
        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<Result>>>(),
                It.IsAny<Func<Result, bool>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<Result>>, Func<Result, bool>, CancellationToken>((operation, shouldCommit, token) =>
            {
                _capturedShouldCommit = shouldCommit;
                return operation(token);
            });
        _lazyTarget = new(CreateInstance<TransactionalPipelineBehavior<TestTransactionalCommand, Result>>);
    }

    [Fact]
    public async Task RunsTheCommandInsideATransaction()
    {
        // Arrange
        PipelineDelegate<Result> next = _ => Task.FromResult(Result.Success());

        // Act
        await Target.HandleAsync(new TestTransactionalCommand(), next, CancellationToken);

        // Assert
        _unitOfWorkMock.Verify(
            u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<Result>>>(),
                It.IsAny<Func<Result, bool>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CommitsWhenTheCommandSucceeds()
    {
        // Arrange
        PipelineDelegate<Result> next = _ => Task.FromResult(Result.Success());

        // Act
        await Target.HandleAsync(new TestTransactionalCommand(), next, CancellationToken);

        // Assert
        _capturedShouldCommit.ShouldNotBeNull();
        _capturedShouldCommit(Result.Success()).ShouldBeTrue();
    }

    [Fact]
    public async Task RollsBackWhenTheCommandReturnsAFailedResult()
    {
        // Arrange
        var failure = Result.Failure(Error.Failure("Test.Failure", "boom"));
        PipelineDelegate<Result> next = _ => Task.FromResult(failure);

        // Act
        var result = await Target.HandleAsync(new TestTransactionalCommand(), next, CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        _capturedShouldCommit.ShouldNotBeNull();
        _capturedShouldCommit(failure).ShouldBeFalse();
    }

    public sealed record TestTransactionalCommand : ITransactionalCommand;
}
