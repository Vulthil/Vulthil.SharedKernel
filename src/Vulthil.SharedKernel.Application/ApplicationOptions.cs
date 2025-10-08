using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Behaviors;
using Vulthil.SharedKernel.Application.Pipeline;

namespace Vulthil.SharedKernel.Application;

public class FluentValidationOptions
{
    private readonly HashSet<Assembly> _fluentValidationAssemblies = [];
    public IReadOnlyList<Assembly> FluentValidationAssemblies => _fluentValidationAssemblies.ToList().AsReadOnly();

    public FluentValidationOptions RegisterFluentValidationAssemblies(params Assembly[] assemblies)
    {
        foreach (var item in assemblies)
        {
            _fluentValidationAssemblies.Add(item);
        }

        return this;
    }
}
public class HandlerOptions
{
    private readonly HashSet<Assembly> _handlerAssemblies = [];
    private readonly List<ServiceDescriptor> _pipelineHandlers = [];
    public IReadOnlyList<Assembly> HandlerAssemblies => _handlerAssemblies.ToList().AsReadOnly();
    public IReadOnlyList<ServiceDescriptor> PipelineHandlers => _pipelineHandlers.AsReadOnly();
    public HandlerOptions RegisterHandlerAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            _handlerAssemblies.Add(assembly);
        }

        return this;
    }

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

public sealed class ApplicationOptions
{
    internal FluentValidationOptions FluentValidationOptions { get; } = new();
    internal HandlerOptions HandlerOptions { get; } = new();
    public IReadOnlyList<Assembly> FluentValidationAssemblies => FluentValidationOptions.FluentValidationAssemblies;
    public IReadOnlyList<Assembly> HandlerAssemblies => HandlerOptions.HandlerAssemblies;

    public IReadOnlyList<ServiceDescriptor> PipelineHandlers => HandlerOptions.PipelineHandlers;

    public ApplicationOptions RegisterHandlerAssemblies(params Assembly[] assemblies)
    {
        HandlerOptions.RegisterHandlerAssemblies(assemblies);
        return this;
    }

    public ApplicationOptions RegisterFluentValidationAssemblies(params Assembly[] assemblies)
    {
        FluentValidationOptions.RegisterFluentValidationAssemblies(assemblies);
        return this;
    }

    public ApplicationOptions AddRequestLoggingBehavior()
    {
        HandlerOptions.AddOpenPipelineHandler(typeof(LoggingBehaviors.RequestLoggingPipelineBehavior<,>));
        return this;
    }

    public ApplicationOptions AddDomainEventLoggingBehavior()
    {
        HandlerOptions.AddOpenDomainEventPipelineHandler(typeof(LoggingBehaviors.DomainEventLoggingPipelineBehavior<>));
        return this;
    }

    public ApplicationOptions AddValidationPipelineBehavior()
    {
        HandlerOptions.AddOpenPipelineHandler(typeof(TransactionalPipelineBehavior<,>));
        return this;
    }

    public ApplicationOptions AddTransactionalPipelineBehavior()
    {
        HandlerOptions.AddOpenPipelineHandler(typeof(ValidationPipelineBehavior<,>));
        return this;
    }
}
