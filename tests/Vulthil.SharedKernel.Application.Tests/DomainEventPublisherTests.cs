using System.Text;
using Vulthil.SharedKernel.Application.Messaging.DomainEvents;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.SharedKernel.Events;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Application.Tests;

/// <summary>
/// Represents the DomainEventPublisherTests.
/// </summary>
public sealed class DomainEventPublisherTests : BaseUnitTestCase
{
    private readonly Lazy<DomainEventPublisher> _lazyTarget;
    private DomainEventPublisher Target => _lazyTarget.Value;
    /// <summary>
    /// Executes this member.
    /// </summary>
    public DomainEventPublisherTests() => _lazyTarget = new(CreateInstance<DomainEventPublisher>);

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public async Task PublishDomainEventNull()
    {
        // Act
        var action = () => Target.PublishAsync(null!, CancellationToken);

        // Assert
        await action.ShouldThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public async Task PublishDomainEventNonDomainEvent()
    {
        // Arrange
        var o = new { };
        // Act
        var action = () => Target.PublishAsync(o, CancellationToken);

        // Assert
        var argumentException = await action.ShouldThrowAsync<ArgumentException>();
        argumentException.Message.ShouldBe($"notification does not implement ${nameof(IDomainEvent)}");
    }

    internal sealed record TestEvent : IDomainEvent;

    internal sealed class TestEventHandler(TextWriter textWriter) : IDomainEventHandler<TestEvent>
    {
        /// <summary>
        /// Executes this member.
        /// </summary>
        public Task HandleAsync(TestEvent notification, CancellationToken cancellationToken = default) => textWriter.WriteLineAsync("Success");
    }
    internal sealed class TestEventHandlerPipeline : IDomainEventPipelineHandler<TestEvent>
    {
        /// <summary>
        /// Executes this member.
        /// </summary>
        public Task HandleAsync(TestEvent domainEvent, DomainEventPipelineDelegate next, CancellationToken cancellationToken = default) => next(cancellationToken);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public async Task PublishDomainEvent()
    {
        // Arrange
        var testEvent = new TestEvent();
        var stringBuilder = new StringBuilder();
        using var stringWriter = new StringWriter(stringBuilder);
        var testEventHandler = new TestEventHandler(stringWriter);
        Use<IServiceProvider>(AutoMocker);
        Use<IDomainEventHandler<TestEvent>>(testEventHandler);
        Use<IDomainEventPipelineHandler<TestEvent>>(new TestEventHandlerPipeline());

        // Act
        await Target.PublishAsync((object)testEvent, CancellationToken);

        // Assert
        stringBuilder.ToString().ShouldContain("Success");
    }
}
