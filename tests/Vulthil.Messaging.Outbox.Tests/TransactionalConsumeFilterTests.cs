using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Transport;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Outbox.Tests;

public sealed class TransactionalConsumeFilterTests : BaseUnitTestCase
{
    [Fact]
    public async Task ConsumeAsyncRunsTheNextDelegateInsideUnitOfWorkExecuteInTransactionAsync()
    {
        // Arrange
        SetUpUnitOfWorkToInvokeTheOperation();
        var filter = CreateInstance<TransactionalConsumeFilter<TestMessage>>();
        var context = NewContext();
        var nextInvoked = false;
        ConsumeDelegate<TestMessage> next = receivedContext =>
        {
            nextInvoked = ReferenceEquals(receivedContext, context);
            return Task.CompletedTask;
        };

        // Act
        await filter.ConsumeAsync(context, next);

        // Assert
        nextInvoked.ShouldBeTrue();
        GetMock<IUnitOfWork>().Verify(
            unitOfWork => unitOfWork.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task<bool>>>(), context.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeAsyncNeverRunsTheNextDelegateWhenTheUnitOfWorkDoesNotInvokeTheOperation()
    {
        // Arrange
        GetMock<IUnitOfWork>()
            .Setup(unitOfWork => unitOfWork.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task<bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var filter = CreateInstance<TransactionalConsumeFilter<TestMessage>>();
        var context = NewContext();
        var nextInvoked = false;
        ConsumeDelegate<TestMessage> next = _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        // Act
        await filter.ConsumeAsync(context, next);

        // Assert
        nextInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task ConsumeAsyncPropagatesAnExceptionThrownByTheNextDelegate()
    {
        // Arrange
        SetUpUnitOfWorkToInvokeTheOperation();
        var filter = CreateInstance<TransactionalConsumeFilter<TestMessage>>();
        var context = NewContext();
        ConsumeDelegate<TestMessage> next = _ => throw new InvalidOperationException("handler failed");

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => filter.ConsumeAsync(context, next));
    }

    private void SetUpUnitOfWorkToInvokeTheOperation() =>
        GetMock<IUnitOfWork>()
            .Setup(unitOfWork => unitOfWork.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task<bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<bool>> operation, CancellationToken token) => operation(token));

    private static MessageContext<TestMessage> NewContext() => new()
    {
        Message = new TestMessage("hello"),
        RoutingKey = string.Empty,
        Headers = new Dictionary<string, object?>(),
        CancellationToken = CancellationToken,
    };

    private sealed record TestMessage(string Value);
}
