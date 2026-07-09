using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Transport;
using Vulthil.SharedKernel.Outbox;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Outbox.Tests;

public sealed class TransactionalPublishFilterTests : BaseUnitTestCase
{
    [Fact]
    public async Task PublishingInsideAnAmbientTransactionScopeWithNoEntityFrameworkTransactionThrowsNotSupportedException()
    {
        // Arrange
        GetMock<IOutboxStore>().Setup(store => store.IsInTransaction).Returns(false);
        var filter = CreateInstance<TransactionalPublishFilter>();
        var context = NewContext();
        var nextInvoked = false;
        PublishFilterDelegate next = _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        // Act
        NotSupportedException exception;
        using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            exception = await Should.ThrowAsync<NotSupportedException>(() => filter.PublishAsync(context, next));
        }

        // Assert
        exception.Message.ShouldContain("TransactionScope");
        exception.Message.ShouldContain("IUnitOfWork.ExecuteInTransactionAsync");
        nextInvoked.ShouldBeFalse();
        GetMock<IOutboxStore>().Verify(store => store.AddOutboxMessage(It.IsAny<OutboxMessage>()), Times.Never);
    }

    [Fact]
    public async Task PublishingWithNoTransactionAndNoAmbientScopePublishesDirectly()
    {
        // Arrange
        GetMock<IOutboxStore>().Setup(store => store.IsInTransaction).Returns(false);
        var filter = CreateInstance<TransactionalPublishFilter>();
        var context = NewContext();
        var nextInvoked = false;
        PublishFilterDelegate next = _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        // Act
        await filter.PublishAsync(context, next);

        // Assert
        nextInvoked.ShouldBeTrue();
        GetMock<IOutboxStore>().Verify(store => store.AddOutboxMessage(It.IsAny<OutboxMessage>()), Times.Never);
    }

    [Fact]
    public async Task PublishingInsideAnEntityFrameworkTransactionCapturesTheMessageInsteadOfThrowing()
    {
        // Arrange
        GetMock<IOutboxStore>().Setup(store => store.IsInTransaction).Returns(true);
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions()));
        Use(TimeProvider.System);
        GetMock<IMessageConfigurationProvider>().Setup(provider => provider.JsonSerializerOptions).Returns(new JsonSerializerOptions());
        var filter = CreateInstance<TransactionalPublishFilter>();
        var context = NewContext();
        var nextInvoked = false;
        PublishFilterDelegate next = _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        // Act
        using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await filter.PublishAsync(context, next);
        }

        // Assert
        nextInvoked.ShouldBeFalse();
        GetMock<IOutboxStore>().Verify(store => store.AddOutboxMessage(It.IsAny<OutboxMessage>()), Times.Once);
        GetMock<IOutboxStore>().Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PublishFilterContext NewContext() => new()
    {
        Message = new TestMessage("hello"),
        MessageType = typeof(TestMessage),
        Context = new PublishContext(),
        Kind = PublishKind.Publish,
        CancellationToken = CancellationToken,
    };

    private sealed record TestMessage(string Value);
}
