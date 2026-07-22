using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Messaging.DomainEvents;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Application.Tests.Pipeline;

/// <summary>
/// Verifies that directly injected handler interfaces resolve to the pipeline-decorated
/// handler and that <see cref="ISender"/> reuses the same composition.
/// </summary>
public sealed class HandlerRegistrationTests : BaseUnitTestCase
{
    [Fact]
    public async Task ResolvingIHandlerReturnsPipelineDecoratedHandler()
    {
        var services = BuildServices(addBehavior: true);
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<IHandler<PingCommand, Result<string>>>();
        var result = await handler.HandleAsync(new PingCommand("hi"), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("[wrapped] hi");
    }

    [Fact]
    public async Task ResolvingICommandHandlerWithResponseReturnsPipelineDecoratedHandler()
    {
        var services = BuildServices(addBehavior: true);
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<PingCommand, Result<string>>>();
        var result = await handler.HandleAsync(new PingCommand("yo"), CancellationToken);

        result.Value.ShouldBe("[wrapped] yo");
    }

    [Fact]
    public async Task ResolvingICommandHandlerUnitVariantReturnsPipelineDecoratedHandler()
    {
        var services = BuildServices(addBehavior: true);
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<TickCommand>>();
        var result = await handler.HandleAsync(new TickCommand(), CancellationToken);

        // The behavior writes a sentinel into the static probe; success implies the inner ran too.
        TestBehavior.WasInvoked.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ResolvingIQueryHandlerReturnsPipelineDecoratedHandler()
    {
        var services = BuildServices(addBehavior: true);
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<EchoQuery, Result<string>>>();
        var result = await handler.HandleAsync(new EchoQuery("echo"), CancellationToken);

        result.Value.ShouldBe("[wrapped] echo");
    }

    [Fact]
    public async Task SenderInvokesPipelineDecoratedHandlerExactlyOnce()
    {
        var services = BuildServices(addBehavior: true);
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        TestBehavior.InvocationCount = 0;
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.SendAsync(new PingCommand("once"), CancellationToken);

        TestBehavior.InvocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task BehaviorsRegisteredAfterHandlersStillApply()
    {
        var services = new ServiceCollection();
        services.AddApplication(o => o.RegisterHandlerAssemblies(typeof(HandlerRegistrationTests).Assembly));
        services.AddApplication(o =>
        {
            o.RegisterHandlerAssemblies(typeof(HandlerRegistrationTests).Assembly);
            o.AddOpenPipelineHandler(typeof(TestBehavior<,>));
        });

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        TestBehavior.InvocationCount = 0;
        var handler = scope.ServiceProvider.GetRequiredService<IHandler<PingCommand, Result<string>>>();
        await handler.HandleAsync(new PingCommand("late"), CancellationToken);

        TestBehavior.InvocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task ConcreteHandlerTypeIsNotResolvableViaDi()
    {
        var services = BuildServices(addBehavior: false);
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var directlyResolved = scope.ServiceProvider.GetService(typeof(PingCommandHandler));

        directlyResolved.ShouldBeNull();
    }

    [Fact]
    public async Task MultipleBehaviorsExecuteInRegistrationOrderEndToEnd()
    {
        // Arrange
        OrderTrackingBehaviors.ExecutionOrder.Clear();
        var services = new ServiceCollection();
        services.AddApplication(o =>
        {
            o.RegisterHandlerAssemblies(typeof(HandlerRegistrationTests).Assembly);
            o.AddOpenPipelineHandler(typeof(FirstOrderTrackingBehavior<,>));
            o.AddOpenPipelineHandler(typeof(SecondOrderTrackingBehavior<,>));
        });
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IHandler<PingCommand, Result<string>>>();

        // Act
        await handler.HandleAsync(new PingCommand("order"), CancellationToken);

        // Assert
        OrderTrackingBehaviors.ExecutionOrder.ShouldBe(["First-Before", "Second-Before", "Second-After", "First-After"]);
    }

    [Fact]
    public async Task AddHandlersAndAddFluentValidationFromASecondModuleComposeAdditively()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddApplication(o => o.RegisterHandlerAssemblies(typeof(HandlerRegistrationTests).Assembly));
        services.AddHandlers(o => o.RegisterHandlerAssemblies(typeof(HandlerRegistrationTests).Assembly));
        services.AddFluentValidation(o => o.RegisterFluentValidationAssemblies(typeof(HandlerRegistrationTests).Assembly));

        // Act
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var hostResult = await sender.SendAsync(new PingCommand("host"), CancellationToken);
        var moduleResult = await sender.SendAsync(new ModuleCommand("module"), CancellationToken);
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<ModuleCommand>>();
        var validationResult = await validator.ValidateAsync(new ModuleCommand(string.Empty), CancellationToken);

        // Assert
        services.Count(d => d.ServiceType == typeof(ISender)).ShouldBe(1);
        services.Count(d => d.ServiceType == typeof(IDomainEventPublisher)).ShouldBe(1);
        hostResult.Value.ShouldBe("host");
        moduleResult.Value.ShouldBe("[module] module");
        validationResult.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task BehaviorWithUnmatchedConstraintIsSkipped()
    {
        var services = new ServiceCollection();
        services.AddApplication(o =>
        {
            o.RegisterHandlerAssemblies(typeof(HandlerRegistrationTests).Assembly);
            // CommandOnlyBehavior only matches ICommand requests — queries should not pick it up.
            o.AddOpenPipelineHandler(typeof(CommandOnlyBehavior<,>));
        });

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<EchoQuery, Result<string>>>();
        var result = await handler.HandleAsync(new EchoQuery("plain"), CancellationToken);

        // Behavior did not wrap the response.
        result.Value.ShouldBe("plain");
    }

    [Fact]
    public async Task HandlerImplementingTwoInterfacesResolvesAndDispatchesBothViaDirectInjection()
    {
        var services = BuildServices(addBehavior: false);
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var commandHandler = scope.ServiceProvider.GetRequiredService<IHandler<MultiHandlerCommand, Result<string>>>();
        var queryHandler = scope.ServiceProvider.GetRequiredService<IHandler<MultiHandlerQuery, Result<string>>>();

        var commandResult = await commandHandler.HandleAsync(new MultiHandlerCommand("cmd"), CancellationToken);
        var queryResult = await queryHandler.HandleAsync(new MultiHandlerQuery("qry"), CancellationToken);

        commandResult.Value.ShouldBe("[command] cmd");
        queryResult.Value.ShouldBe("[query] qry");
    }

    [Fact]
    public async Task HandlerImplementingTwoInterfacesResolvesAndDispatchesBothViaSender()
    {
        var services = BuildServices(addBehavior: false);
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var commandResult = await sender.SendAsync(new MultiHandlerCommand("cmd"), CancellationToken);
        var queryResult = await sender.SendAsync(new MultiHandlerQuery("qry"), CancellationToken);

        commandResult.Value.ShouldBe("[command] cmd");
        queryResult.Value.ShouldBe("[query] qry");
    }

    [Fact]
    public async Task AsyncDisposableHandlerIsDisposedWhenAsyncScopeEnds()
    {
        AsyncDisposableCommandHandler.Reset();
        var services = BuildServices(addBehavior: false);
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IHandler<AsyncDisposableCommand, Result>>();
            await handler.HandleAsync(new AsyncDisposableCommand(), CancellationToken);
        }

        AsyncDisposableCommandHandler.WasDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposableHandlerIsDisposedWhenScopeEnds()
    {
        SyncDisposableCommandHandler.Reset();
        var services = BuildServices(addBehavior: false);
        await using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IHandler<SyncDisposableCommand, Result>>();
            await handler.HandleAsync(new SyncDisposableCommand(), CancellationToken);
        }

        SyncDisposableCommandHandler.WasDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task BareTransactionalCommandIsWrappedByTheTransactionalBehavior()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<Result>>>(),
                It.IsAny<Func<Result, bool>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<Result>>, Func<Result, bool>, CancellationToken>((operation, _, token) => operation(token));

        var services = new ServiceCollection();
        services.AddSingleton(unitOfWork.Object);
        services.AddApplication(o =>
        {
            o.RegisterHandlerAssemblies(typeof(HandlerRegistrationTests).Assembly);
            o.AddTransactionalPipelineBehavior();
        });
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new BareTransactionalCommand(), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        unitOfWork.Verify(
            u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<Result>>>(),
                It.IsAny<Func<Result, bool>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ServiceCollection BuildServices(bool addBehavior)
    {
        var services = new ServiceCollection();
        services.AddApplication(o =>
        {
            o.RegisterHandlerAssemblies(typeof(HandlerRegistrationTests).Assembly);
            if (addBehavior)
            {
                o.AddOpenPipelineHandler(typeof(TestBehavior<,>));
            }
        });
        return services;
    }

    internal sealed record ModuleCommand(string Message) : ICommand<Result<string>>;

    internal sealed class ModuleCommandHandler : ICommandHandler<ModuleCommand, Result<string>>
    {
        public Task<Result<string>> HandleAsync(ModuleCommand request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success("[module] " + request.Message));
    }

    internal sealed class ModuleCommandValidator : AbstractValidator<ModuleCommand>
    {
        public ModuleCommandValidator() => RuleFor(x => x.Message).NotEmpty();
    }

    internal static class OrderTrackingBehaviors
    {
        public static List<string> ExecutionOrder { get; } = [];
    }

    internal sealed class FirstOrderTrackingBehavior<TRequest, TResponse> : IPipelineHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default)
        {
            OrderTrackingBehaviors.ExecutionOrder.Add("First-Before");
            var response = await next(cancellationToken);
            OrderTrackingBehaviors.ExecutionOrder.Add("First-After");
            return response;
        }
    }

    internal sealed class SecondOrderTrackingBehavior<TRequest, TResponse> : IPipelineHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default)
        {
            OrderTrackingBehaviors.ExecutionOrder.Add("Second-Before");
            var response = await next(cancellationToken);
            OrderTrackingBehaviors.ExecutionOrder.Add("Second-After");
            return response;
        }
    }
}

internal static class TestBehavior
{
    public static int InvocationCount;
    public static bool WasInvoked => InvocationCount > 0;
}

internal sealed class TestBehavior<TRequest, TResponse> : IPipelineHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref TestBehavior.InvocationCount);
        var response = await next(cancellationToken);

        // Decorate string-bearing Result<string> responses so tests can observe the behavior ran.
        if (response is Result<string> { IsSuccess: true } stringResult)
        {
            return (TResponse)(object)Result.Success("[wrapped] " + stringResult.Value);
        }

        return response;
    }
}

internal sealed class CommandOnlyBehavior<TCommand, TResponse> : IPipelineHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public Task<TResponse> HandleAsync(TCommand request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default) =>
        next(cancellationToken);
}

public sealed record BareTransactionalCommand : ITransactionalCommand;

internal sealed class BareTransactionalCommandHandler : ICommandHandler<BareTransactionalCommand>
{
    public Task<Result> HandleAsync(BareTransactionalCommand request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}

public sealed record PingCommand(string Message) : ICommand<Result<string>>;

internal sealed class PingCommandHandler : ICommandHandler<PingCommand, Result<string>>
{
    public Task<Result<string>> HandleAsync(PingCommand request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(request.Message));
}

public sealed record TickCommand : ICommand;

internal sealed class TickCommandHandler : ICommandHandler<TickCommand>
{
    public Task<Result> HandleAsync(TickCommand request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}

public sealed record EchoQuery(string Value) : IQuery<Result<string>>;

internal sealed class EchoQueryHandler : IQueryHandler<EchoQuery, Result<string>>
{
    public Task<Result<string>> HandleAsync(EchoQuery request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(request.Value));
}

public sealed record MultiHandlerCommand(string Message) : ICommand<Result<string>>;

public sealed record MultiHandlerQuery(string Message) : IQuery<Result<string>>;

internal sealed class MultiHandlerHandler :
    ICommandHandler<MultiHandlerCommand, Result<string>>,
    IQueryHandler<MultiHandlerQuery, Result<string>>
{
    public Task<Result<string>> HandleAsync(MultiHandlerCommand request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success("[command] " + request.Message));

    public Task<Result<string>> HandleAsync(MultiHandlerQuery request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success("[query] " + request.Message));
}

public sealed record AsyncDisposableCommand : ICommand;

internal sealed class AsyncDisposableCommandHandler : ICommandHandler<AsyncDisposableCommand>, IAsyncDisposable
{
    public static bool WasDisposed { get; private set; }

    public static void Reset() => WasDisposed = false;

    public Task<Result> HandleAsync(AsyncDisposableCommand request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());

    public ValueTask DisposeAsync()
    {
        WasDisposed = true;
        return ValueTask.CompletedTask;
    }
}

public sealed record SyncDisposableCommand : ICommand;

internal sealed class SyncDisposableCommandHandler : ICommandHandler<SyncDisposableCommand>, IDisposable
{
    public static bool WasDisposed { get; private set; }

    public static void Reset() => WasDisposed = false;

    public Task<Result> HandleAsync(SyncDisposableCommand request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());

    public void Dispose() => WasDisposed = true;
}
