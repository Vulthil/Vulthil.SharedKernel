using Vulthil.SharedKernel.Outbox;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Outbox.Tests;

public sealed class BrokerOutboxDispatcherTests : BaseUnitTestCase
{
    [Fact]
    public async Task DispatchAsyncThrowsDescriptiveErrorWhenMessageTypeCannotBeResolved()
    {
        // Arrange
        var dispatcher = CreateInstance<BrokerOutboxDispatcher>();
        var message = new OutboxMessageData(
            Id: Guid.NewGuid(),
            Type: "Vulthil.Nonexistent.PhantomMessage",
            Content: "{}",
            TraceParent: null,
            TraceState: null,
            Destination: OutboxDestination.Publish,
            Metadata: null);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(message, CancellationToken));

        // Assert
        exception.Message.ShouldContain("Vulthil.Nonexistent.PhantomMessage");
    }
}
