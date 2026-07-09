using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vulthil.Messaging.Outbox;

/// <summary>
/// Round-trips <see cref="BrokerOutboxMetadata.Headers"/> through the persisted metadata JSON so a relayed
/// publish re-serializes to the same wire bytes as the captured one: primitives rematerialize as the CLR
/// types the consume side normalizes to (<see cref="string"/>, <see cref="bool"/>, and <see cref="int"/>/
/// <see cref="long"/>/<see cref="double"/> — the narrowest that represents the value), and non-primitive
/// values rematerialize as detached <see cref="JsonElement"/>s, which serialize back verbatim.
/// </summary>
internal sealed class BrokerOutboxMetadataHeadersConverter : JsonConverter<Dictionary<string, object?>>
{
    public override Dictionary<string, object?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        var headers = new Dictionary<string, object?>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return headers;
            }

            var key = reader.GetString()!;
            reader.Read();
            headers[key] = ReadValue(ref reader);
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, object?> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var header in value)
        {
            writer.WritePropertyName(header.Key);
            JsonSerializer.Serialize(writer, header.Value, options);
        }

        writer.WriteEndObject();
    }

    private static object? ReadValue(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var intValue))
                {
                    return intValue;
                }

                if (reader.TryGetInt64(out var longValue))
                {
                    return longValue;
                }

                return reader.GetDouble();
            default:
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    return document.RootElement.Clone();
                }
        }
    }
}
