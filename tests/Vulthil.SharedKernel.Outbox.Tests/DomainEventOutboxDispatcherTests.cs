using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

public sealed class DomainEventOutboxDispatcherTests : BaseUnitTestCase
{
    [Fact]
    public async Task DispatchAsyncThrowsDescriptiveErrorWhenDomainEventTypeCannotBeResolved()
    {
        // Arrange
        var dispatcher = CreateInstance<DomainEventOutboxDispatcher>();
        var message = new OutboxMessageData(
            Id: Guid.NewGuid(),
            Type: "Vulthil.Nonexistent.PhantomEvent",
            Content: "{}",
            TraceParent: null,
            TraceState: null,
            Destination: OutboxDestination.DomainEvent,
            Metadata: null);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(message, CancellationToken));

        // Assert
        exception.Message.ShouldContain("Vulthil.Nonexistent.PhantomEvent");
    }
}
