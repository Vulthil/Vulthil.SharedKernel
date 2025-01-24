using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.xUnit;

namespace Vulthil.SharedKernel.Tests;

public sealed class IDomainEventHandlerTests : BaseUnitTestCase
{
    private sealed record TestDomainEvent : IDomainEvent;
    private sealed class TestDomainHandler : IDomainEventHandler<TestDomainEvent>
    {
        public Task HandleAsync(TestDomainEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task DomainHandlerShouldForwardINotificationHandlerCalls()
    {
        // Arrange
        var domainEvent = new TestDomainEvent();
        IDomainEventHandler<TestDomainEvent> handler = new TestDomainHandler();
        CancellationToken cancellationToken = CancellationToken.None;

        // Act
        Func<Task> act = async () => await handler.HandleAsync(domainEvent, cancellationToken);

        // Assert
        await act.ShouldNotThrowAsync();
    }
}
