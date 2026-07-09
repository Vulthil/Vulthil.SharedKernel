using System.Text;

namespace Vulthil.Messaging.RabbitMq;

internal static class RabbitMqConstants
{
    public const string ContentType = "application/json";

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

        return Uri.TryCreate(str, UriKind.Absolute, out var uri)
            ? uri
            : new Uri($"queue:{str}");
    }
}
