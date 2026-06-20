using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Service-collection extensions that register the provider-agnostic outbox engine — the relay processor, the
/// background service, the commit-time signal, and the in-process domain-event sink. A persistence host (e.g.
/// <c>Vulthil.SharedKernel.Infrastructure</c>) calls this and then registers an <see cref="IOutboxStore"/>
/// implementation for its provider.
/// </summary>
public static class OutboxEngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers the outbox engine: <see cref="OutboxProcessingOptions"/> (validated on start), the commit-time
    /// <see cref="IOutboxSignal"/>, the relay <see cref="OutboxProcessor"/> and its background service, and the
    /// default in-process <see cref="IOutboxDispatcher"/> for domain events. An <see cref="IOutboxStore"/> must be
    /// registered separately by the persistence provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An optional action to configure <see cref="OutboxProcessingOptions"/>.</param>
    /// <param name="processorLifetime">
    /// The lifetime for the relay processor and the in-process dispatcher; match the application's
    /// <c>DbContext</c> lifetime so they resolve the store in the same scope.
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddOutboxEngine(
        this IServiceCollection services,
        Action<OutboxProcessingOptions>? configureOptions = null,
        ServiceLifetime processorLifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<OutboxProcessingOptions>()
            .Configure(configureOptions ?? (static _ => { }))
            .Validate(
                o => !o.Retention.Enabled
                    || o.Retention.RetentionPeriod > TimeSpan.Zero
                    && o.Retention.SweepInterval > TimeSpan.Zero
                    && o.Retention.BatchSize >= 1,
                "Outbox retention requires RetentionPeriod and SweepInterval greater than zero and BatchSize of at least 1 when enabled.")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var options = new OutboxProcessingOptions();
        configureOptions?.Invoke(options);

        if (options.EnableTracing)
        {
            services
                .AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddVulthilOutboxInstrumentation());
        }

        if (options.EnableMetrics)
        {
            services
                .AddOpenTelemetry()
                .WithMetrics(metrics => metrics.AddVulthilOutboxInstrumentation());
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOutboxSignal, OutboxSignal>();

        services.Add(new ServiceDescriptor(typeof(OutboxProcessor), typeof(OutboxProcessor), processorLifetime));
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IOutboxDispatcher), typeof(DomainEventOutboxDispatcher), processorLifetime));

        services.AddHostedService<OutboxBackgroundService>();

        if (options.Retention.Enabled)
        {
            services.AddHostedService<OutboxRetentionBackgroundService>();
        }

        return services;
    }
}
