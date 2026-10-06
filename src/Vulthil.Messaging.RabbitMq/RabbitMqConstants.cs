using System.Globalization;
using System.Text;
using System.Text.Json;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq;

internal static class RabbitMqConstants
{
    public const string ContentType = "application/json";

    public const string RetryCountHeader = "x-retry-count";

    public const string RetryHandlersHeader = "x-retry-handlers";

    /// <summary>
    /// Reads the round a delivery starts at from its <see cref="RetryCountHeader"/> header. The worker writes the
    /// round as an <see cref="int"/>, but a message replayed from the management UI or sent by another client can
    /// carry it as another integer type or as text. A whole number from 0 to <see cref="int.MaxValue"/> counts in
    /// any of these forms. Any other value counts as 0, a first delivery, so the consumers still run; a retry
    /// re-publish writes the round back as an <see cref="int"/>.
    /// </summary>
    public static int GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(RetryCountHeader, out var value))
        {
            return 0;
        }

        return value switch
        {
            int count and >= 0 => count,
            long count and >= 0 and <= int.MaxValue => (int)count,
            _ => int.TryParse(
                AsText(value),
                NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture,
                out var count) ? count : 0,
        };
    }

    /// <summary>
    /// Reads the handler identities stamped on a delayed-retry re-delivery by
    /// <see cref="SerializeRetryHandlerIdentities"/>. Returns <see langword="null"/> when the delivery carries
    /// none (a first delivery, an external producer, or an unparsable value) — the caller then dispatches the
    /// full plan.
    /// </summary>
    public static IReadOnlyList<string>? GetRetryHandlerIdentities(IDictionary<string, object?>? headers)
    {
        var raw = headers is null ? null : GetHeaderString(headers, RetryHandlersHeader);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Serializes handler identities for the <see cref="RetryHandlersHeader"/> header as a JSON string array,
    /// which round-trips identities containing arbitrary characters (e.g. generic type names).
    /// </summary>
    public static string SerializeRetryHandlerIdentities(IEnumerable<string> identities)
        => JsonSerializer.Serialize(identities);

    /// <summary>
    /// Formats <paramref name="delay"/> as a per-message AMQP TTL (the <c>expiration</c> property). The broker
    /// accepts only a whole, non-negative number of milliseconds and closes the channel on any other value, so a
    /// fractional delay (such as a jittered retry interval) rounds up to the next whole millisecond and a negative
    /// delay counts as zero. <see cref="TryParseExpiration"/> reads the value back.
    /// </summary>
    public static string FormatExpiration(TimeSpan delay)
        => ((long)Math.Ceiling(Math.Max(0d, delay.TotalMilliseconds))).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Maps a per-message AMQP TTL to an absolute expiration instant. The TTL is relative to when the message
    /// was published, so it is anchored to <paramref name="sentTime"/> when the delivery carries a timestamp;
    /// without one the consume-side clock is the only anchor available and the result is an upper bound.
    /// </summary>
    public static DateTimeOffset? TryParseExpiration(string? expiration, DateTimeOffset? sentTime)
    {
        if (!string.IsNullOrWhiteSpace(expiration) && long.TryParse(expiration, out var ms))
        {
            try
            {
                return (sentTime ?? DateTimeOffset.UtcNow).AddMilliseconds(ms);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTimeOffset.MaxValue;
            }
        }

        return null;
    }

    public static string? GetHeaderString(IDictionary<string, object?> headers, string key)
        => headers.TryGetValue(key, out var value) ? AsText(value) : null;

    public static Uri? GetHeaderUri(IDictionary<string, object?> headers, string key) =>
        MessageAddress.Parse(GetHeaderString(headers, key));

    /// <summary>
    /// Reads a header value as text. The client surfaces an AMQP string as its UTF-8 bytes, so a byte array decodes
    /// back to the string; any other value is formatted.
    /// </summary>
    private static string? AsText(object? value)
        => value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value?.ToString();
}
