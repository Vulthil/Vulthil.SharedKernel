namespace Storefront.Api;

/// <summary>
/// The Shipments service serializes its status enum as a number; these are its names in order.
/// </summary>
public static class ShipmentStatusNames
{
    private static readonly string[] Names = ["Created", "Picked up", "In transit", "Delivered"];

    public static string Of(int status) => status >= 0 && status < Names.Length ? Names[status] : "Unknown";
}
