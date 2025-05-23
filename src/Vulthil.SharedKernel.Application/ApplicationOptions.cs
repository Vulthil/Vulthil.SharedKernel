using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Behaviors;
using Vulthil.SharedKernel.Application.Pipeline;

namespace Vulthil.SharedKernel.Application;

public sealed class ApplicationOptions
{
    private readonly HashSet<Assembly> _fluentValidationAssemblies = [];
    private readonly HashSet<Assembly> _mediatRAssemblies = [];
    private readonly List<ServiceDescriptor> _pipelineHandlers = [];

    public IReadOnlyList<Assembly> FluentValidationAssemblies => _fluentValidationAssemblies.ToList().AsReadOnly();
    public IReadOnlyList<Assembly> MediatRAssemblies => _mediatRAssemblies.ToList().AsReadOnly();

    public IReadOnlyList<ServiceDescriptor> PipelineHandlers => _pipelineHandlers.AsReadOnly();

    public ApplicationOptions RegisterMediatRAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            _mediatRAssemblies.Add(assembly);
        }

        return this;
    }

    public ApplicationOptions RegisterFluentValidationAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            _fluentValidationAssemblies.Add(assembly);
        }

        return this;
    }

    public ApplicationOptions AddRequestLoggingBehavior() => AddOpenPipelineHandler(typeof(RequestLoggingPipelineBehavior<,>));
    public ApplicationOptions AddValidationPipelineBehavior() => AddOpenPipelineHandler(typeof(TransactionalPipelineBehavior<,>));
    public ApplicationOptions AddTransactionalPipelineBehavior() => AddOpenPipelineHandler(typeof(ValidationPipelineBehavior<,>));

    public ApplicationOptions AddOpenPipelineHandler(Type pipelineHandler)
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
