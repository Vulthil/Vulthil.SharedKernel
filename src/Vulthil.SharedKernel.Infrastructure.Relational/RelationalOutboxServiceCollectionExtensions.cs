using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure.Relational;

/// <summary>
/// Service-collection extensions for relational outbox providers.
/// </summary>
public static class RelationalOutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the relational transaction interceptor that reports every transaction to
    /// <see cref="OutboxRelayWakeup"/>, so the outbox relay wakes as soon as a transaction that saved outbox rows
    /// commits (low-latency delivery) while a commit that saved none, such as the relay's own batch, never wakes it.
    /// The periodic poll remains the correctness backstop. Relational provider packages call this when outbox
    /// processing is enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddRelationalOutboxCommitTrigger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<OutboxRelayWakeup>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOutboxInterceptor, OutboxCommitInterceptor>());

        return services;
    }
}
