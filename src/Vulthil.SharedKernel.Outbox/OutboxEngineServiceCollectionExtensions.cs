using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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
    /// registered separately by the persistence provider. The engine's own services are registered idempotently, so
    /// calling this repeatedly (e.g. once per <c>EnableOutboxProcessing</c> call) never duplicates them; only one
    /// <see cref="IOutboxStore"/> is supported per host, because the relay and retention services resolve a single
    /// instance, so a second one being already registered fails fast instead of orphaning the first context's outbox.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An optional action to configure <see cref="OutboxProcessingOptions"/>.</param>
    /// <param name="processorLifetime">
    /// The lifetime for the relay processor and the in-process dispatcher; match the application's
    /// <c>DbContext</c> lifetime so they resolve the store in the same scope.
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// An <see cref="IOutboxStore"/> is already registered for a different context.
    /// </exception>
    public static IServiceCollection AddOutboxEngine(
        this IServiceCollection services,
        Action<OutboxProcessingOptions>? configureOptions = null,
        ServiceLifetime processorLifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);

        var existingStore = services.FirstOrDefault(static descriptor => descriptor.ServiceType == typeof(IOutboxStore));
        if (existingStore is not null)
        {
            var existingContextName = existingStore.ImplementationType?.IsGenericType == true
                ? existingStore.ImplementationType.GetGenericArguments()[0].Name
                : existingStore.ImplementationType?.Name ?? "an existing DbContext";

            throw new InvalidOperationException(
                $"Outbox processing is already enabled for '{existingContextName}'. Only one outbox-enabled DbContext " +
                "is supported per host, because the outbox relay and retention services resolve a single IOutboxStore. " +
                "Enabling it again for a second context would leave that context's outbox messages unrelayed with no diagnostic.");
        }

        services.AddOptions<OutboxProcessingOptions>()
            .Configure(configureOptions ?? (static _ => { }))
            .Validate(
                o => !o.Retention.Enabled
                    || o.Retention.RetentionPeriod > TimeSpan.Zero
                    && o.Retention.SweepInterval > TimeSpan.Zero
                    && o.Retention.BatchSize >= 1,
                "Outbox retention requires RetentionPeriod and SweepInterval greater than zero and BatchSize of at least 1 when enabled.")
            .Validate(
                o => o.MaxDelaySeconds >= o.OutboxProcessingDelaySeconds,
                "MaxDelaySeconds must be greater than or equal to OutboxProcessingDelaySeconds.")
            .Validate(
                o => o.OutboxProcessingDelaySeconds is >= 1 and <= 100,
                "OutboxProcessingDelaySeconds must be between 1 and 100.")
            .Validate(
                o => o.MaxDelaySeconds is >= 1 and <= 300,
                "MaxDelaySeconds must be between 1 and 300.")
            .Validate(
                o => o.BatchSize >= 1,
                "BatchSize must be at least 1.")
            .Validate(
                o => o.MaxRetries >= 1,
                "MaxRetries must be at least 1.")
            .Validate(
                o => o.MaxDegreeOfParallelism is >= 1 and <= 100,
                "MaxDegreeOfParallelism must be between 1 and 100.")
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

        services.TryAdd(new ServiceDescriptor(typeof(OutboxProcessor), typeof(OutboxProcessor), processorLifetime));
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IOutboxDispatcher), typeof(DomainEventOutboxDispatcher), processorLifetime));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxBackgroundService>());

        if (options.Retention.Enabled)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxRetentionBackgroundService>());
        }

        return services;
    }
}
