using System.Collections.Concurrent;
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

    [Fact]
    public async Task ALaterHandlerStillRunsWhenAnEarlierHandlerFails()
    {
        // Arrange
        var log = new ConcurrentQueue<string>();
        await using var provider = HandlersFor(
            new ScriptedHandler("first", log, _ => throw new InvalidOperationException("first failed")),
            new ScriptedHandler("second", log));
        Use<IServiceProvider>(provider);

        // Act
        var exception = await Should.ThrowAsync<AggregateException>(() => Target.PublishAsync(new ScriptedEvent(), CancellationToken));

        // Assert
        log.ShouldBe(["first", "second"]);
        exception.InnerExceptions.ShouldHaveSingleItem().Message.ShouldBe("first failed");
    }

    [Fact]
    public async Task EveryFailureComesInOneAggregateExceptionInHandlerOrder()
    {
        // Arrange
        var log = new ConcurrentQueue<string>();
        await using var provider = HandlersFor(
            new ScriptedHandler("first", log, _ => throw new InvalidOperationException("first failed")),
            new ScriptedHandler("second", log, _ => throw new InvalidOperationException("second failed")));
        Use<IServiceProvider>(provider);

        // Act
        var exception = await Should.ThrowAsync<AggregateException>(() => Target.PublishAsync(new ScriptedEvent(), CancellationToken));

        // Assert
        exception.InnerExceptions.Select(inner => inner.Message).ShouldBe(["first failed", "second failed"]);
    }

    [Fact]
    public async Task ACancelledTokenStopsTheHandlersAndComesThroughUnwrapped()
    {
        // Arrange
        var log = new ConcurrentQueue<string>();
        using var publishCancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        await using var provider = HandlersFor(
            new ScriptedHandler("first", log, async token =>
            {
                await publishCancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
            }),
            new ScriptedHandler("second", log));
        Use<IServiceProvider>(provider);

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => Target.PublishAsync(new ScriptedEvent(), publishCancellation.Token));

        // Assert
        log.ShouldBe(["first"]);
    }

    [Fact]
    public async Task ACancellationInTheLastHandlerComesThroughUnwrapped()
    {
        // Arrange
        var log = new ConcurrentQueue<string>();
        using var publishCancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        await using var provider = HandlersFor(
            new ScriptedHandler("first", log),
            new ScriptedHandler("last", log, async token =>
            {
                await publishCancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
            }));
        Use<IServiceProvider>(provider);

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => Target.PublishAsync(new ScriptedEvent(), publishCancellation.Token));

        // Assert
        log.ShouldBe(["first", "last"]);
    }

    [Fact]
    public async Task ACancellationAfterAFailedHandlerStillWins()
    {
        // Arrange
        var log = new ConcurrentQueue<string>();
        using var publishCancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        await using var provider = HandlersFor(
            new ScriptedHandler("first", log, async _ =>
            {
                await publishCancellation.CancelAsync();
                throw new InvalidOperationException("first failed");
            }),
            new ScriptedHandler("second", log));
        Use<IServiceProvider>(provider);

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => Target.PublishAsync(new ScriptedEvent(), publishCancellation.Token));

        // Assert
        log.ShouldBe(["first"]);
    }

    [Fact]
    public async Task AHandlersOwnCancellationWhileTheTokenIsLiveIsAnOrdinaryFailure()
    {
        // Arrange
        var log = new ConcurrentQueue<string>();
        await using var provider = HandlersFor(
            new ScriptedHandler("first", log, _ => throw new TaskCanceledException("the handler's own timeout")),
            new ScriptedHandler("second", log));
        Use<IServiceProvider>(provider);

        // Act
        var exception = await Should.ThrowAsync<AggregateException>(() => Target.PublishAsync(new ScriptedEvent(), CancellationToken));

        // Assert
        log.ShouldBe(["first", "second"]);
        exception.InnerExceptions.ShouldHaveSingleItem().ShouldBeOfType<TaskCanceledException>();
    }

    [Fact]
    public async Task AnAlreadyCancelledTokenRunsNoHandler()
    {
        // Arrange
        var log = new ConcurrentQueue<string>();
        using var publishCancellation = new CancellationTokenSource();
        await publishCancellation.CancelAsync();
        await using var provider = HandlersFor(new ScriptedHandler("first", log));
        Use<IServiceProvider>(provider);

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => Target.PublishAsync(new ScriptedEvent(), publishCancellation.Token));

        // Assert
        log.ShouldBeEmpty();
    }

    [Fact]
    public async Task PipelineHandlersWrapTheWholeSetOfHandlers()
    {
        // Arrange
        var log = new ConcurrentQueue<string>();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<ScriptedEvent>>(new ScriptedHandler("first", log));
        services.AddSingleton<IDomainEventHandler<ScriptedEvent>>(new ScriptedHandler("second", log));
        services.AddSingleton<IDomainEventPipelineHandler<ScriptedEvent>>(new LoggingPipelineHandler(log));
        await using var provider = services.BuildServiceProvider();
        Use<IServiceProvider>(provider);

        // Act
        await Target.PublishAsync(new ScriptedEvent(), CancellationToken);

        // Assert
        log.ShouldBe(["before", "first", "second", "after"]);
    }

    [Fact]
    public async Task TheEventsRuntimeTypePicksTheHandlers()
    {
        // Arrange
        var log = new ConcurrentQueue<string>();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<BaseScriptedEvent>>(new BaseScriptedEventHandler(log));
        services.AddSingleton<IDomainEventHandler<DerivedScriptedEvent>>(new DerivedScriptedEventHandler(log));
        await using var provider = services.BuildServiceProvider();
        Use<IServiceProvider>(provider);

        // Act
        await Target.PublishAsync<BaseScriptedEvent>(new DerivedScriptedEvent(), CancellationToken);

        // Assert
        log.ShouldBe(["derived"]);
    }

    private static ServiceProvider HandlersFor(params IDomainEventHandler<ScriptedEvent>[] handlers)
    {
        var services = new ServiceCollection();
        foreach (var handler in handlers)
        {
            services.AddSingleton(handler);
        }

        return services.BuildServiceProvider();
    }

    internal sealed record ScriptedEvent : IDomainEvent;

    internal record BaseScriptedEvent : IDomainEvent;

    internal sealed record DerivedScriptedEvent : BaseScriptedEvent;

    /// <summary>A handler that logs its name, then runs its script, if any.</summary>
    internal sealed class ScriptedHandler(string name, ConcurrentQueue<string> log, Func<CancellationToken, Task>? script = null)
        : IDomainEventHandler<ScriptedEvent>
    {
        public async Task HandleAsync(ScriptedEvent notification, CancellationToken cancellationToken = default)
        {
            log.Enqueue(name);
            if (script is not null)
            {
                await script(cancellationToken);
            }
        }
    }

    internal sealed class LoggingPipelineHandler(ConcurrentQueue<string> log) : IDomainEventPipelineHandler<ScriptedEvent>
    {
        public async Task HandleAsync(ScriptedEvent domainEvent, DomainEventPipelineDelegate next, CancellationToken cancellationToken = default)
        {
            log.Enqueue("before");
            await next(cancellationToken);
            log.Enqueue("after");
        }
    }

    internal sealed class BaseScriptedEventHandler(ConcurrentQueue<string> log) : IDomainEventHandler<BaseScriptedEvent>
    {
        public Task HandleAsync(BaseScriptedEvent notification, CancellationToken cancellationToken = default)
        {
            log.Enqueue("base");
            return Task.CompletedTask;
        }
    }

    internal sealed class DerivedScriptedEventHandler(ConcurrentQueue<string> log) : IDomainEventHandler<DerivedScriptedEvent>
    {
        public Task HandleAsync(DerivedScriptedEvent notification, CancellationToken cancellationToken = default)
        {
            log.Enqueue("derived");
            return Task.CompletedTask;
        }
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
