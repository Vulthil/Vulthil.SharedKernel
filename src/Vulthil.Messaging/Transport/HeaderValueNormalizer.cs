using System.Text.Json;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Normalizes header values that crossed the JSON wire so consumers observe stable CLR primitives instead of
/// <see cref="JsonElement"/> wrappers: strings, booleans, and numbers unwrap to <see cref="string"/>,
/// <see cref="bool"/>, and <see cref="int"/>/<see cref="long"/>/<see cref="double"/> (the narrowest of the
/// three that represents the value). Objects and arrays stay <see cref="JsonElement"/>, and values that never
/// crossed the wire (in-memory transports) pass through unchanged, so the normalization is idempotent.
/// </summary>
internal static class HeaderValueNormalizer
{
    public static object? Normalize(object? value) => value switch
    {
        JsonElement element => NormalizeElement(element),
        _ => value,
    };

    private static object? NormalizeElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.Number => NormalizeNumber(element),
        _ => element,
    };

    private static object NormalizeNumber(JsonElement element)
    {
        if (element.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        if (element.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        return element.GetDouble();
    }
}
