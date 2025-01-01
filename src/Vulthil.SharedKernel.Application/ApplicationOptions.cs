using System.Collections.Generic;
using System.Reflection;

namespace Vulthil.SharedKernel.Application;

public sealed class ApplicationOptions
{
    private readonly HashSet<Assembly> _fluentValidationAssemblies = [];
    private readonly HashSet<Assembly> _mediatRAssemblies = [];

    public IReadOnlyList<Assembly> FluentValidationAssemblies => _fluentValidationAssemblies.ToList().AsReadOnly();
    public IReadOnlyList<Assembly> MediatRAssemblies => _mediatRAssemblies.ToList().AsReadOnly();

    public bool AddValidationPipelineBehaviour { get; set; } = true;
    public bool AddTransactionalPipelineBehaviour { get; set; } = true;

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
}
