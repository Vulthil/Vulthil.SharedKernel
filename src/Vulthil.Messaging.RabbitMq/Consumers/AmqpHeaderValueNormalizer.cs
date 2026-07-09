using System.Text;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// Normalizes received AMQP header values so the bare-AMQP compat path surfaces the same CLR primitives as the
/// envelope path: the client wire-encodes every string header as a UTF-8 byte array, so byte arrays decode back
/// to <see cref="string"/>, and nested tables/arrays are normalized recursively. Values the client already
/// surfaces as typed primitives (<see cref="int"/>, <see cref="long"/>, <see cref="bool"/>, ...) pass through
/// unchanged.
/// </summary>
internal static class AmqpHeaderValueNormalizer
{
    public static Dictionary<string, object?> Normalize(IDictionary<string, object?> headers)
    {
        var normalized = new Dictionary<string, object?>(headers.Count);
        foreach (var (key, value) in headers)
        {
            normalized[key] = NormalizeValue(value);
        }

        return normalized;
    }

    private static object? NormalizeValue(object? value) => value switch
    {
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        List<object?> list => list.ConvertAll(NormalizeValue),
        IDictionary<string, object?> table => Normalize(table),
        _ => value,
    };
}
