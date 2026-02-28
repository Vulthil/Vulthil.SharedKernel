using System.Reflection;
using System.Text;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.RabbitMq;

internal static class RabbitMqConstants
{
    public const string ContentType = "application/json";

    public static string? GetMetadata(Type type, object message, IReadOnlyDictionary<Type, Func<object, string>> registry)
    {
        var current = type;
        while (current != null && current != typeof(object))
        {
            if (registry.TryGetValue(current, out var picker))
            {
                return picker(message);
            }

            current = current.BaseType; // Walk up the inheritance tree
        }
        return null;
    }

    public static string GetRoutingKey(Registration registration) =>
       registration.ConsumerType.Type.GetCustomAttribute<RoutingKeyAttribute>()?.Pattern ?? registration.RoutingKey;

    public static int GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers?.TryGetValue("x-retry-count", out var countObj) == true)
        {
            return countObj switch
            {
                int i => i,
                byte[] b => BitConverter.ToInt32(b),
                long l => (int)l,
                _ => 0
            };
        }

        return 0;
    }
    public static DateTimeOffset? TryParseExpiration(string? expiration)
    {
        // RabbitMQ stores expiration as a string representing milliseconds
        if (!string.IsNullOrWhiteSpace(expiration) && long.TryParse(expiration, out var ms))
        {
            try
            {
                // We calculate expiration relative to 'now' when we received it
                return DateTimeOffset.UtcNow.AddMilliseconds(ms);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Handle cases where ms might be too large for DateTimeOffset
                return DateTimeOffset.MaxValue;
            }
        }

        return null;
    }

    public static string? GetHeaderString(IDictionary<string, object?> headers, string key)
    {
        if (headers.TryGetValue(key, out var value) && value is byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return value?.ToString();
    }

    public static Uri? GetHeaderUri(IDictionary<string, object?> headers, string key)
    {
        var str = GetHeaderString(headers, key);
        if (string.IsNullOrWhiteSpace(str))
        {
            return null;
        }

        // Try to parse as absolute (e.g., amqp://broker/queue) 
        // or fall back to a custom scheme (e.g., queue:my-reply-queue)
        return Uri.TryCreate(str, UriKind.Absolute, out var uri)
            ? uri
            : new Uri($"queue:{str}");
    }
}
