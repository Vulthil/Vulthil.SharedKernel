using FluentAssertions;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Tests;

public sealed class IDomainEventHandlerTests
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
        await act.Should().NotThrowAsync();
    }
}
