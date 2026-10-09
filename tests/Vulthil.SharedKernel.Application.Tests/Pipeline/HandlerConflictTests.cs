using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Events;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Application.Tests.Pipeline;

public sealed class HandlerConflictTests : BaseUnitTestCase<ServiceCollection>
{
    protected override ServiceCollection CreateInstance() => new();

    [Fact]
    public void TwoHandlersForOneRequestInOneAssemblyThrow()
    {
        // Arrange
        var assembly = new TypesAssembly(typeof(FirstHandler<ConflictCommand>), typeof(SecondHandler<ConflictCommand>));

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => Target.AddHandlers(o => o.RegisterHandlerAssemblies(assembly)));
        exception.Message.ShouldContain(typeof(ConflictCommand).ToString());
        exception.Message.ShouldContain(typeof(Result<string>).ToString());
        exception.Message.ShouldContain(typeof(FirstHandler<ConflictCommand>).ToString());
        exception.Message.ShouldContain(typeof(SecondHandler<ConflictCommand>).ToString());
    }

    [Theory]
    [InlineData(typeof(FirstHandler<ConflictCommand>), typeof(SecondHandler<ConflictCommand>))]
    [InlineData(typeof(SecondHandler<ConflictCommand>), typeof(FirstHandler<ConflictCommand>))]
    public void AnotherHandlerForTheSameRequestFromASecondModuleThrows(Type firstModuleHandler, Type secondModuleHandler)
    {
        // Arrange
        Target.AddHandlers(o => o.RegisterHandlerAssemblies(new TypesAssembly(firstModuleHandler)));

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            Target.AddHandlers(o => o.RegisterHandlerAssemblies(new TypesAssembly(secondModuleHandler))));
        exception.Message.ShouldContain(firstModuleHandler.ToString());
        exception.Message.ShouldContain(secondModuleHandler.ToString());
    }

    [Fact]
    public void AHandlerDerivedFromAConcreteHandlerForTheSameRequestThrows()
    {
        // Arrange
        var assembly = new TypesAssembly(typeof(FirstHandler<ConflictCommand>), typeof(DerivedHandler<ConflictCommand>));

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => Target.AddHandlers(o => o.RegisterHandlerAssemblies(assembly)));
        exception.Message.ShouldContain(typeof(DerivedHandler<ConflictCommand>).ToString());
    }

    [Fact]
    public async Task TheSameHandlerFromTwoModulesIsRegisteredOnce()
    {
        // Arrange
        Target.AddHandlers(o => o.RegisterHandlerAssemblies(new TypesAssembly(typeof(FirstHandler<ConflictCommand>))));
        Target.AddHandlers(o => o.RegisterHandlerAssemblies(new TypesAssembly(typeof(FirstHandler<ConflictCommand>))));
        await using var provider = Target.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Act
        var result = await sender.SendAsync(new ConflictCommand(), CancellationToken);

        // Assert
        result.Value.ShouldBe("first");
        Target.Count(descriptor => descriptor.ServiceType == typeof(IHandler<ConflictCommand, Result<string>>)).ShouldBe(1);
    }

    [Fact]
    public async Task TwoHandlersForOneDomainEventAreBothRegistered()
    {
        // Arrange
        Target.AddHandlers(o => o.RegisterHandlerAssemblies(new TypesAssembly(typeof(FirstEventHandler), typeof(SecondEventHandler))));
        await using var provider = Target.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var handlers = scope.ServiceProvider.GetServices<IDomainEventHandler<ConflictEvent>>();

        // Assert
        handlers.Select(handler => handler.GetType()).ShouldBe([typeof(FirstEventHandler), typeof(SecondEventHandler)], ignoreOrder: true);
    }

    [Fact]
    public void ATypeThatFailsToLoadInAHandlerAssemblyThrowsWithTheLoaderErrors()
    {
        // Arrange
        var loadError = new ReflectionTypeLoadException(
            [typeof(FirstHandler<ConflictCommand>), null],
            [null, new FileNotFoundException("Could not load file or assembly 'Missing.Dependency'.")]);
        var assembly = new TypeLoadFailingAssembly(loadError);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => Target.AddHandlers(o => o.RegisterHandlerAssemblies(assembly)));
        exception.Message.ShouldContain(assembly.FullName);
        exception.Message.ShouldContain("Could not load file or assembly 'Missing.Dependency'.");
        exception.InnerException.ShouldBeSameAs(loadError);
    }

    internal sealed record ConflictCommand : ICommand<Result<string>>;

    internal sealed record ConflictEvent : IDomainEvent;

    // The handler scan skips generic definitions, so the scans of this whole test assembly in other tests never see
    // these competing handlers; only a test that hands their closed types to a TypesAssembly registers them.
    internal class FirstHandler<TCommand> : ICommandHandler<TCommand, Result<string>>
        where TCommand : ICommand<Result<string>>
    {
        public virtual Task<Result<string>> HandleAsync(TCommand request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success("first"));
    }

    internal sealed class DerivedHandler<TCommand> : FirstHandler<TCommand>
        where TCommand : ICommand<Result<string>>
    {
        public override Task<Result<string>> HandleAsync(TCommand request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success("derived"));
    }

    internal sealed class SecondHandler<TCommand> : ICommandHandler<TCommand, Result<string>>
        where TCommand : ICommand<Result<string>>
    {
        public Task<Result<string>> HandleAsync(TCommand request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success("second"));
    }

    internal sealed class FirstEventHandler : IDomainEventHandler<ConflictEvent>
    {
        public Task HandleAsync(ConflictEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class SecondEventHandler : IDomainEventHandler<ConflictEvent>
    {
        public Task HandleAsync(ConflictEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TypesAssembly(params Type[] types) : Assembly
    {
        public override string FullName => "HandlerConflictTests.Handlers";

        public override Type[] GetTypes() => types;
    }

    private sealed class TypeLoadFailingAssembly(ReflectionTypeLoadException loadError) : Assembly
    {
        public override string FullName => "HandlerConflictTests.TypeLoadFailing";

        public override Type[] GetTypes() => throw loadError;
    }
}
