using Vulthil.Results;

namespace Orders.ExternalServices;

public static class ExternalServiceErrors
{
    public static Error Unavailable(string service) =>
        Error.Failure("ExternalService.Unavailable", $"The {service} service did not answer.");

    public static Error UnknownSku(string sku) =>
        Error.NotFound("Inventory.UnknownSku", $"The inventory has no SKU {sku}.");
}
