using System.Diagnostics;
using System.Reflection;

namespace Vulthil.Messaging.RabbitMq.Telemetry;

/// <summary>
/// Provides the <see cref="System.Diagnostics.ActivitySource"/> emitted by the Vulthil RabbitMQ transport.
/// </summary>
/// <remarks>
/// Register this source with OpenTelemetry by calling
/// <c>tracerProviderBuilder.AddVulthilMessagingInstrumentation()</c> or by adding the source name directly:
/// <c>tracerProviderBuilder.AddSource(MessagingInstrumentation.ActivitySourceName)</c>.
/// </remarks>
public static class MessagingInstrumentation
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.ActivitySource"/> used by the Vulthil RabbitMQ transport.
    /// </summary>
    public const string ActivitySourceName = "Vulthil.Messaging.RabbitMq";

    internal static readonly ActivitySource ActivitySource = new(
        ActivitySourceName,
        typeof(MessagingInstrumentation).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(MessagingInstrumentation).Assembly.GetName().Version?.ToString()
            ?? "0.0.0");

    internal static class Tags
    {
        public const string MessagingSystem = "messaging.system";
        public const string MessagingOperation = "messaging.operation";
        public const string MessagingDestination = "messaging.destination.name";
        public const string MessagingMessageId = "messaging.message.id";
        public const string MessagingConversationId = "messaging.message.conversation_id";
        public const string MessagingCorrelationId = "messaging.rabbitmq.correlation_id";
        public const string MessagingRoutingKey = "messaging.rabbitmq.routing_key";
        public const string MessageType = "vulthil.messaging.message_type";
        public const string ConsumerType = "vulthil.messaging.consumer_type";
        public const string QueueName = "vulthil.messaging.queue";
        public const string RetryCount = "vulthil.messaging.retry_count";
    }

    internal const string SystemValue = "rabbitmq";
}
