using Vulthil.SharedKernel.Events;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Tests.Core;

/// <summary>
/// Represents the IDomainEventHandlerTests.
/// </summary>
public sealed class IDomainEventHandlerTests : BaseUnitTestCase
{
    private sealed record TestDomainEvent : IDomainEvent;
    private sealed class TestDomainHandler : IDomainEventHandler<TestDomainEvent>
    {
        /// <summary>
        /// Executes this member.
        /// </summary>
        public Task HandleAsync(TestDomainEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public async Task DomainHandlerShouldHandleAsync()
    {
        // Arrange
        var domainEvent = new TestDomainEvent();
        IDomainEventHandler<TestDomainEvent> handler = new TestDomainHandler();
        var cancellationToken = CancellationToken.None;

        // Act
        Func<Task> act = () => handler.HandleAsync(domainEvent, cancellationToken);

        // Assert
        await act.ShouldNotThrowAsync();
    }
}
