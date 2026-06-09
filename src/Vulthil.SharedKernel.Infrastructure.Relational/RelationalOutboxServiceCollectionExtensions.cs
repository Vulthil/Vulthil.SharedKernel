using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;
using Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure.Relational;

/// <summary>
/// Service-collection extensions for relational outbox providers.
/// </summary>
public static class RelationalOutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the relational transaction-commit interceptor that wakes the outbox relay as soon as a transaction
    /// commits (low-latency delivery), keeping the periodic poll as the correctness backstop. Relational provider
    /// packages call this when outbox processing is enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddRelationalOutboxCommitTrigger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOutboxInterceptor, OutboxCommitInterceptor>());

        return services;
    }
}
