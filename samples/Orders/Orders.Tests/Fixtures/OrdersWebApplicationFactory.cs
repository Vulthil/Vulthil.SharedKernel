using Orders.Endpoints;
using Orders.ExternalServices;
using Vulthil.xUnit;

namespace Orders.Tests.Fixtures;

/// <summary>
/// Boots the Orders sample against the shared <see cref="OrdersContainerHost"/> containers: each test class gets its
/// own PostgreSQL database and RabbitMQ virtual host, so classes using this factory run in parallel without
/// interfering. The two external HTTP services are replaced by HTTP mocks that each test configures.
/// </summary>
public sealed class OrdersWebApplicationFactory : BaseWebApplicationFactory<Program>
{
    public OrdersWebApplicationFactory(OrdersContainerHost containerHost) : base(containerHost)
    {
        AddHttpMock<IShippingRatesClient>();
        AddHttpMock(GetStock.InventoryClientName);
    }
}
