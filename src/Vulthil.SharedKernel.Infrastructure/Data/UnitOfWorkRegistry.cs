using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Infrastructure.Data;

/// <summary>
/// Records the contexts registered with <c>AddDbContext</c>, in registration order, and owns the host's one
/// <see cref="IUnitOfWork"/> registration: the context itself when there is one, or a <see cref="CompositeUnitOfWork"/>
/// over all of them.
/// </summary>
internal sealed class UnitOfWorkRegistry
{
    private readonly List<Type> _contextTypes = [];
    private Type? _outboxContextType;
    private ServiceDescriptor? _descriptor;

    /// <summary>
    /// Records <paramref name="contextType"/> as part of the host's unit of work. Recording the same context again
    /// adds nothing, but can mark it as the outbox context.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="contextType">The registered context type.</param>
    /// <param name="contextLifetime">The lifetime the context was registered with.</param>
    /// <param name="isOutboxContext">Whether outbox processing is enabled for the context.</param>
    public static void Register(IServiceCollection services, Type contextType, ServiceLifetime contextLifetime, bool isOutboxContext)
    {
        var registry = services
            .Where(static descriptor => descriptor.ServiceType == typeof(UnitOfWorkRegistry) && !descriptor.IsKeyedService)
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<UnitOfWorkRegistry>()
            .FirstOrDefault();
        if (registry is null)
        {
            registry = new UnitOfWorkRegistry();
            services.AddSingleton(registry);
        }

        registry.Add(services, contextType, contextLifetime, isOutboxContext);
    }

    private void Add(IServiceCollection services, Type contextType, ServiceLifetime contextLifetime, bool isOutboxContext)
    {
        if (isOutboxContext)
        {
            _outboxContextType = contextType;
        }

        if (_contextTypes.Contains(contextType))
        {
            return;
        }

        _contextTypes.Add(contextType);

        // A single context is its own unit of work and keeps its lifetime; the composite depends on every context, so
        // it is scoped.
        var descriptor = new ServiceDescriptor(
            typeof(IUnitOfWork),
            Create,
            _contextTypes.Count == 1 ? contextLifetime : ServiceLifetime.Scoped);

        // Replacing the earlier registration in place keeps an IUnitOfWork that the host registered after it in front.
        var index = _descriptor is null ? -1 : services.IndexOf(_descriptor);
        if (index >= 0)
        {
            services[index] = descriptor;
        }
        else
        {
            services.Add(descriptor);
        }

        _descriptor = descriptor;
    }

    private IUnitOfWork Create(IServiceProvider services)
    {
        if (_contextTypes.Count == 1)
        {
            return (IUnitOfWork)services.GetRequiredService(_contextTypes[0]);
        }

        var contexts = _contextTypes.Select(type => (DbContext)services.GetRequiredService(type)).ToList();
        var outboxContext = _outboxContextType is null ? null : contexts[_contextTypes.IndexOf(_outboxContextType)];
        return new CompositeUnitOfWork(contexts, outboxContext);
    }
}
