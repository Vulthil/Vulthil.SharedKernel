using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.xUnit;

namespace Orders.Tests.Fixtures;

public abstract class OrdersTestCase(OrdersWebApplicationFactory factory, ITestOutputHelper testOutputHelper) : BaseIntegrationTestCase<Program>(factory, testOutputHelper), IClassFixture<OrdersWebApplicationFactory>
{
    protected ISender Sender => ScopedServices.GetRequiredService<ISender>();
}
