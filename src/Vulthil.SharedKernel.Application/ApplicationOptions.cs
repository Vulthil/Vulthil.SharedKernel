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
    private readonly HashSet<Assembly> _fluentValidationAssemblies = [];
    /// <summary>
    /// Gets the assemblies registered for FluentValidation validator scanning.
    /// Validators discovered in these assemblies are registered automatically.
    /// </summary>
    public IReadOnlyList<Assembly> FluentValidationAssemblies => _fluentValidationAssemblies.ToList().AsReadOnly();

    /// <summary>
    /// Registers assemblies to scan for FluentValidation validators.
    /// </summary>
    /// <param name="assemblies">The assemblies to register.</param>
    /// <returns>The current options instance for chaining.</returns>
    public FluentValidationOptions RegisterFluentValidationAssemblies(params Assembly[] assemblies)
    {
        foreach (var item in assemblies)
        {
            _fluentValidationAssemblies.Add(item);
        }

        return this;
    }
}
/// <summary>
/// Configuration options for handler assembly scanning and pipeline registration.
/// </summary>
public class HandlerOptions
{
    private readonly HashSet<Assembly> _handlerAssemblies = [];
    private readonly List<ServiceDescriptor> _pipelineHandlers = [];
    /// <summary>
    /// Gets the assemblies registered for handler scanning.
    /// Request handlers and domain event handlers in these assemblies are registered automatically.
    /// </summary>
    public IReadOnlyList<Assembly> HandlerAssemblies => _handlerAssemblies.ToList().AsReadOnly();
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
        foreach (var assembly in assemblies)
        {
            _handlerAssemblies.Add(assembly);
        }

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
        if (!pipelineHandler.IsGenericType)
        {
            throw new InvalidOperationException($"{pipelineHandler.Name} must be generic");
        }

        var implementedGenericInterfaces = pipelineHandler.GetInterfaces().Where(i => i.IsGenericType).Select(i => i.GetGenericTypeDefinition());
        var implementedOpenBehaviorInterfaces = new HashSet<Type>(implementedGenericInterfaces.Where(i => i == typeof(IDomainEventPipelineHandler<>)));

        if (implementedOpenBehaviorInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{pipelineHandler.Name} must implement {typeof(IDomainEventPipelineHandler<>).FullName}");
        }

        foreach (var openBehaviorInterface in implementedOpenBehaviorInterfaces)
        {
            _pipelineHandlers.Add(new ServiceDescriptor(openBehaviorInterface, pipelineHandler, ServiceLifetime.Scoped));
        }

        return this;
    }

    /// <summary>
    /// Registers an open-generic request pipeline handler type.
    /// </summary>
    /// <param name="pipelineHandler">The open-generic type implementing <see cref="IPipelineHandler{TRequest, TResponse}"/>.</param>
    /// <returns>The current options instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="pipelineHandler"/> is not a valid open-generic type.</exception>
    public HandlerOptions AddOpenPipelineHandler(Type pipelineHandler)
    {
        if (!pipelineHandler.IsGenericType)
        {
            throw new InvalidOperationException($"{pipelineHandler.Name} must be generic");
        }

        var implementedGenericInterfaces = pipelineHandler.GetInterfaces().Where(i => i.IsGenericType).Select(i => i.GetGenericTypeDefinition());
        var implementedOpenBehaviorInterfaces = new HashSet<Type>(implementedGenericInterfaces.Where(i => i == typeof(IPipelineHandler<,>)));

        if (implementedOpenBehaviorInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{pipelineHandler.Name} must implement {typeof(IPipelineHandler<,>).FullName}");
        }

        foreach (var openBehaviorInterface in implementedOpenBehaviorInterfaces)
        {
            _pipelineHandlers.Add(new ServiceDescriptor(openBehaviorInterface, pipelineHandler, ServiceLifetime.Scoped));
        }

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
    /// Gets the assemblies registered for FluentValidation validator scanning.
    /// Delegates to the internal <see cref="FluentValidationOptions"/>.
    /// </summary>
    public IReadOnlyList<Assembly> FluentValidationAssemblies => FluentValidationOptions.FluentValidationAssemblies;
    /// <summary>
    /// Gets the assemblies registered for handler scanning.
    /// Delegates to the internal <see cref="HandlerOptions"/>.
    /// </summary>
    public IReadOnlyList<Assembly> HandlerAssemblies => HandlerOptions.HandlerAssemblies;

    /// <summary>
    /// Gets the pipeline handler service descriptors for both request and domain event pipelines.
    /// </summary>
    public IReadOnlyList<ServiceDescriptor> PipelineHandlers => HandlerOptions.PipelineHandlers;

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
    /// Adds the validation pipeline behavior that validates commands before execution.
    /// </summary>
    /// <returns>The current options instance for chaining.</returns>
    public ApplicationOptions AddValidationPipelineBehavior()
    {
        HandlerOptions.AddOpenPipelineHandler(typeof(TransactionalPipelineBehavior<,>));
        return this;
    }

    /// <summary>
    /// Adds the transactional pipeline behavior that wraps transactional commands in a database transaction.
    /// </summary>
    /// <returns>The current options instance for chaining.</returns>
    public ApplicationOptions AddTransactionalPipelineBehavior()
    {
        HandlerOptions.AddOpenPipelineHandler(typeof(ValidationPipelineBehavior<,>));
        return this;
    }
}
