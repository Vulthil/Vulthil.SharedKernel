using System.Diagnostics;
using System.Reflection;

namespace Vulthil.Messaging.RabbitMq;

/// <summary>
/// Provides the <see cref="System.Diagnostics.ActivitySource"/> emitted by the Vulthil RabbitMQ transport.
/// </summary>
/// <remarks>
/// Register this source with OpenTelemetry by calling
/// <c>tracerProviderBuilder.AddVulthilRabbitMqInstrumentation()</c> or by adding the source name directly:
/// <c>tracerProviderBuilder.AddSource(RabbitMqTelemetry.ActivitySourceName)</c>.
/// </remarks>
public static class RabbitMqTelemetry
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.ActivitySource"/> used by the Vulthil RabbitMQ transport.
    /// </summary>
    public const string ActivitySourceName = "Vulthil.Messaging.RabbitMq";

    internal static readonly ActivitySource ActivitySource = new(
        ActivitySourceName,
        typeof(RabbitMqTelemetry).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(RabbitMqTelemetry).Assembly.GetName().Version?.ToString()
            ?? "0.0.0");

    internal static class Tags
    {
        public const string MessagingSystem = "messaging.system";
        public const string MessagingOperation = "messaging.operation";
        public const string MessagingDestination = "messaging.destination.name";
        public const string MessagingMessageId = "messaging.message.id";
        public const string MessagingCorrelationId = "messaging.rabbitmq.correlation_id";
        public const string MessagingRoutingKey = "messaging.rabbitmq.routing_key";
        public const string MessageType = "vulthil.messaging.message_type";
        public const string QueueName = "vulthil.messaging.queue";
        public const string RetryCount = "vulthil.messaging.retry_count";
    }

    internal const string SystemValue = "rabbitmq";
}
