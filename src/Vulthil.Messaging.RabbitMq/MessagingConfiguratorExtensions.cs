using Aspire.RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.HealthChecks;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Messaging.RabbitMq.Requests;
using Vulthil.Messaging.RabbitMq.Sending;
using Vulthil.Messaging.RabbitMq.Telemetry;

namespace Vulthil.Messaging.RabbitMq;

/// <summary>
/// Extension methods for configuring the RabbitMQ transport on an <see cref="IMessagingConfigurator"/>.
/// </summary>
public static class MessagingConfiguratorExtensions
{
    /// <summary>
    /// Configures the messaging infrastructure to use RabbitMQ as the transport.
    /// </summary>
    /// <param name="configurator">The messaging configurator.</param>
    /// <param name="connectionStringKey">The Aspire connection-string key for the RabbitMQ resource.</param>
    /// <param name="configureSettings">
    /// Optional callback for tuning the Aspire client settings.
    /// Setting <see cref="RabbitMQClientSettings.DisableTracing"/> to <see langword="true"/> also suppresses the Vulthil transport's
    /// activity source, so the entire RabbitMQ tracing pipeline (Aspire client + Vulthil transport) can be toggled with a single flag.
    /// </param>
    /// <param name="configureConnectionFactory">Optional callback for tuning the underlying <see cref="ConnectionFactory"/>.</param>
    /// <returns>The same configurator, for chaining.</returns>
    public static IMessagingConfigurator UseRabbitMq(
        this IMessagingConfigurator configurator,
        string connectionStringKey = "rabbitMq",
        Action<RabbitMQClientSettings>? configureSettings = null,
        Action<ConnectionFactory>? configureConnectionFactory = null)
    {
        var tracingEnabled = true;
        var healthChecksEnabled = true;

        configurator.HostApplicationBuilder.AddRabbitMQClient(
            connectionStringKey,
            settings =>
            {
                configureSettings?.Invoke(settings);
                tracingEnabled = !settings.DisableTracing;
                healthChecksEnabled = !settings.DisableHealthChecks;
            },
            configureConnectionFactory);

        var services = configurator.HostApplicationBuilder.Services;

        services.AddSingleton<RabbitMqBusStartupStatus>();
        services.AddSingleton<RabbitMqBus>();
        services.AddSingleton<ITransport>(sp => sp.GetRequiredService<RabbitMqBus>());

        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<IPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());
        services.AddSingleton<IInternalPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());

        services.AddSingleton<ISendEndpointProvider, RabbitMqSendEndpointProvider>();

        // ResponseListener is lazily initialized on the first IRequester.RequestAsync call.
        // Services that never make request/reply calls do not pay the cost of declaring a reply queue.
        services.AddSingleton<ResponseListener>();
        services.AddScoped<IRequester, RabbitMqRequester>();

        if (tracingEnabled)
        {
            services
                .AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddVulthilMessagingInstrumentation());
        }

        if (healthChecksEnabled)
        {
            services
                .AddHealthChecks()
                .AddCheck<RabbitMqBusHealthCheck>(
                    name: "vulthil_messaging_rabbitmq_bus",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: ["ready", "messaging", "rabbitmq"]);
        }

        return configurator;
    }
}
