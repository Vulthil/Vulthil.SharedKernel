using System.Text.Json;

namespace Storefront.Api;

/// <summary>
/// The Orders service serializes the fulfillment status as a polymorphic union with a <c>$type</c> discriminator; the
/// page only shows which variant it is.
/// </summary>
public sealed record FulfillmentView(Guid Id, Guid OrderId, JsonElement Status)
{
    public string StatusName =>
        Status.ValueKind == JsonValueKind.Object && Status.TryGetProperty("$type", out var type)
            ? type.GetString() ?? "Unknown"
            : "Unknown";
}
