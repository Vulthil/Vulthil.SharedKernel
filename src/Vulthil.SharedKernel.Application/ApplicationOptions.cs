using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Behaviors;
using Vulthil.SharedKernel.Application.Pipeline;

namespace Vulthil.SharedKernel.Application;

/// <summary>
/// Configuration options for FluentValidation assembly scanning.
/// </summary>
public class FluentValidationOptions
{
    private readonly List<Assembly> _fluentValidationAssemblies = [];

    /// <summary>
    /// Gets the assemblies registered for FluentValidation validator scanning.
    /// Validators discovered in these assemblies are registered automatically.
    /// </summary>
    public IReadOnlyList<Assembly> FluentValidationAssemblies => _fluentValidationAssemblies.AsReadOnly();

    /// <summary>
    /// Registers assemblies to scan for FluentValidation validators.
    /// </summary>
    /// <param name="assemblies">The assemblies to register.</param>
    /// <returns>The current options instance for chaining.</returns>
    public FluentValidationOptions RegisterFluentValidationAssemblies(params Assembly[] assemblies)
    {
        _fluentValidationAssemblies.AddRange(assemblies.Except(_fluentValidationAssemblies));
        return this;
    }
}
/// <summary>
/// Configuration options for handler assembly scanning and pipeline registration.
/// </summary>
public class HandlerOptions
{
    private readonly List<Assembly> _handlerAssemblies = [];
    private readonly List<ServiceDescriptor> _pipelineHandlers = [];
    /// <summary>
    /// Gets the assemblies registered for handler scanning.
    /// Request handlers and domain event handlers in these assemblies are registered automatically.
    /// </summary>
    public IReadOnlyList<Assembly> HandlerAssemblies => _handlerAssemblies.AsReadOnly();
    /// <summary>
    /// Gets the pipeline handler service descriptors registered via <see cref="AddOpenPipelineHandler"/> or <see cref="AddOpenDomainEventPipelineHandler"/>.
    /// </summary>
    public IReadOnlyList<ServiceDescriptor> PipelineHandlers => _pipelineHandlers.AsReadOnly();
    /// <summary>
    /// Registers assemblies to scan for request and domain event handlers.
    /// </summary>
    /// <param name="assemblies">The assemblies to register.</param>
    /// <returns>The current options instance for chaining.</returns>
    public HandlerOptions RegisterHandlerAssemblies(params Assembly[] assemblies)
    {
        _handlerAssemblies.AddRange(assemblies.Except(_handlerAssemblies));
        return this;
    }

    /// <summary>
    /// Registers an open-generic domain event pipeline handler type.
    /// </summary>
    /// <param name="pipelineHandler">The open-generic type implementing <see cref="IDomainEventPipelineHandler{T}"/>.</param>
    /// <returns>The current options instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="pipelineHandler"/> is not a valid open-generic type.</exception>
    public HandlerOptions AddOpenDomainEventPipelineHandler(Type pipelineHandler)
    {
        _pipelineHandlers.Add(OpenGenericPipelineHandler.CreateDescriptor(pipelineHandler, typeof(IDomainEventPipelineHandler<>)));
        return this;
    }

    /// <summary>
    /// Registers an open-generic request pipeline handler type. Pipeline handlers run in registration order: the first
    /// one registered is the outermost.
    /// </summary>
    /// <param name="pipelineHandler">The open-generic type implementing <see cref="IPipelineHandler{TRequest, TResponse}"/>.</param>
    /// <returns>The current options instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="pipelineHandler"/> is not a valid open-generic type.</exception>
    public HandlerOptions AddOpenPipelineHandler(Type pipelineHandler)
    {
        _pipelineHandlers.Add(OpenGenericPipelineHandler.CreateDescriptor(pipelineHandler, typeof(IPipelineHandler<,>)));
        return this;
    }

}

/// <summary>
/// Aggregated configuration options for the application layer, combining handler and validation settings.
/// </summary>
public sealed class ApplicationOptions
{
    internal FluentValidationOptions FluentValidationOptions { get; } = new();
    internal HandlerOptions HandlerOptions { get; } = new();

    /// <summary>
    /// Registers assemblies to scan for request and domain event handlers.
    /// </summary>
    /// <param name="assemblies">The assemblies to register.</param>
    /// <returns>The current options instance for chaining.</returns>
    public ApplicationOptions RegisterHandlerAssemblies(params Assembly[] assemblies)
    {
        HandlerOptions.RegisterHandlerAssemblies(assemblies);
        return this;
    }

    /// <summary>
    /// Registers assemblies to scan for FluentValidation validators.
    /// </summary>
    /// <param name="assemblies">The assemblies to register.</param>
    /// <returns>The current options instance for chaining.</returns>
    public ApplicationOptions RegisterFluentValidationAssemblies(params Assembly[] assemblies)
    {
        FluentValidationOptions.RegisterFluentValidationAssemblies(assemblies);
        return this;
    }

    /// <summary>
    /// Adds the request logging pipeline behavior.
    /// </summary>
    /// <returns>The current options instance for chaining.</returns>
    public ApplicationOptions AddRequestLoggingBehavior()
    {
        HandlerOptions.AddOpenPipelineHandler(typeof(LoggingBehaviors.RequestLoggingPipelineBehavior<,>));
        return this;
    }

    /// <summary>
    /// Adds the domain event logging pipeline behavior.
    /// </summary>
    /// <returns>The current options instance for chaining.</returns>
    public ApplicationOptions AddDomainEventLoggingBehavior()
    {
        HandlerOptions.AddOpenDomainEventPipelineHandler(typeof(LoggingBehaviors.DomainEventLoggingPipelineBehavior<>));
        return this;
    }

    /// <summary>
    /// Adds the validation pipeline behavior that validates commands with FluentValidation before execution. On a
    /// validation failure, a command returning <see cref="Vulthil.Results.Result"/> or <c>Result&lt;T&gt;</c> receives a
    /// failed result containing a <see cref="Vulthil.Results.ValidationError"/>; a command with any other response type
    /// throws a <see cref="CommandValidationException"/>, a <see cref="FluentValidation.ValidationException"/> that
    /// carries the same error.
    /// </summary>
    /// <returns>The current options instance for chaining.</returns>
    public ApplicationOptions AddValidationPipelineBehavior()
    {
        HandlerOptions.AddOpenPipelineHandler(typeof(ValidationPipelineBehavior<,>));
        return this;
    }

    /// <summary>
    /// Adds the transactional pipeline behavior that wraps transactional commands in a database transaction.
    /// </summary>
    /// <returns>The current options instance for chaining.</returns>
    public ApplicationOptions AddTransactionalPipelineBehavior()
    {
        HandlerOptions.AddOpenPipelineHandler(typeof(TransactionalPipelineBehavior<,>));
        return this;
    }

    /// <summary>
    /// Registers an open-generic request pipeline handler type. Pipeline handlers run in registration order: the first
    /// one registered is the outermost.
    /// </summary>
    /// <param name="pipelineHandler">The open-generic type implementing <see cref="Pipeline.IPipelineHandler{TRequest, TResponse}"/>.</param>
    /// <returns>The current options instance for chaining.</returns>
    public ApplicationOptions AddOpenPipelineHandler(Type pipelineHandler)
    {
        HandlerOptions.AddOpenPipelineHandler(pipelineHandler);
        return this;
    }

    /// <summary>
    /// Registers an open-generic domain event pipeline handler type.
    /// </summary>
    /// <param name="pipelineHandler">The open-generic type implementing <see cref="Pipeline.IDomainEventPipelineHandler{TDomainEvent}"/>.</param>
    /// <returns>The current options instance for chaining.</returns>
    public ApplicationOptions AddOpenDomainEventPipelineHandler(Type pipelineHandler)
    {
        HandlerOptions.AddOpenDomainEventPipelineHandler(pipelineHandler);
        return this;
    }
}
