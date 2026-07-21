using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Messaging.DomainEvents;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.SharedKernel.Events;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Application.Tests;

public sealed class DomainEventPublisherTests : BaseUnitTestCase
{
    private readonly Lazy<DomainEventPublisher> _lazyTarget;
    private DomainEventPublisher Target => _lazyTarget.Value;
    public DomainEventPublisherTests() => _lazyTarget = new(CreateInstance<DomainEventPublisher>);

    [Fact]
    public async Task PublishDomainEventNull()
    {
        // Act
        var action = () => Target.PublishAsync(null!, CancellationToken);

        // Assert
        await action.ShouldThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PublishDomainEventNonDomainEvent()
    {
        // Arrange
        var o = new { };
        // Act
        var action = () => Target.PublishAsync(o, CancellationToken);

        // Assert
        var argumentException = await action.ShouldThrowAsync<ArgumentException>();
        argumentException.Message.ShouldBe($"notification does not implement {nameof(IDomainEvent)}");
    }

    internal sealed record TestEvent : IDomainEvent;

    internal sealed class TestEventHandler(TextWriter textWriter) : IDomainEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent notification, CancellationToken cancellationToken = default) => textWriter.WriteLineAsync("Success");
    }
    internal sealed class TestEventHandlerPipeline : IDomainEventPipelineHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent domainEvent, DomainEventPipelineDelegate next, CancellationToken cancellationToken = default) => next(cancellationToken);
    }

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

    [Fact]
    public async Task MultiplePipelineHandlersExecuteInRegistrationOrderEndToEnd()
    {
        // Arrange
        OrderedPipelineHandlers.ExecutionOrder.Clear();
        var services = new ServiceCollection();
        services.AddApplication(o =>
        {
            o.RegisterHandlerAssemblies(typeof(DomainEventPublisherTests).Assembly);
            o.AddOpenDomainEventPipelineHandler(typeof(FirstOrderedPipelineHandler<>));
            o.AddOpenDomainEventPipelineHandler(typeof(SecondOrderedPipelineHandler<>));
        });
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();

        // Act
        await publisher.PublishAsync(new OrderedEvent(), CancellationToken);

        // Assert
        OrderedPipelineHandlers.ExecutionOrder.ShouldBe(["First-Before", "Second-Before", "Second-After", "First-After"]);
    }

    internal sealed record OrderedEvent : IDomainEvent;

    internal sealed class OrderedEventHandler : IDomainEventHandler<OrderedEvent>
    {
        public Task HandleAsync(OrderedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal static class OrderedPipelineHandlers
    {
        public static List<string> ExecutionOrder { get; } = [];
    }

    internal sealed class FirstOrderedPipelineHandler<TDomainEvent> : IDomainEventPipelineHandler<TDomainEvent>
        where TDomainEvent : IDomainEvent
    {
        public async Task HandleAsync(TDomainEvent domainEvent, DomainEventPipelineDelegate next, CancellationToken cancellationToken = default)
        {
            OrderedPipelineHandlers.ExecutionOrder.Add("First-Before");
            await next(cancellationToken);
            OrderedPipelineHandlers.ExecutionOrder.Add("First-After");
        }
    }

    internal sealed class SecondOrderedPipelineHandler<TDomainEvent> : IDomainEventPipelineHandler<TDomainEvent>
        where TDomainEvent : IDomainEvent
    {
        public async Task HandleAsync(TDomainEvent domainEvent, DomainEventPipelineDelegate next, CancellationToken cancellationToken = default)
        {
            OrderedPipelineHandlers.ExecutionOrder.Add("Second-Before");
            await next(cancellationToken);
            OrderedPipelineHandlers.ExecutionOrder.Add("Second-After");
        }
    }
}
