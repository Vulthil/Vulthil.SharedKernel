using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shipments.Application.Notifications;
using Vulthil.Extensions.Testing;
using Vulthil.xUnit;

namespace Shipments.Tests.Fixtures;

/// <summary>
/// Boots the Shipments service against the shared MySQL server and RabbitMQ broker. The carrier simulator is switched
/// off so a test publishes exactly the scans it asserts on, and the customer notifier records instead of sending.
/// </summary>
public class ShipmentsWebApplicationFactory(ShipmentsContainerHost containerHost) : BaseWebApplicationFactory<Program>(containerHost)
{
    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Carrier:SimulatorEnabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerNotifier>();
            services.AddSingleton<InMemoryCustomerNotifier>();
            services.AddSingleton<ICustomerNotifier>(sp => sp.GetRequiredService<InMemoryCustomerNotifier>());
            services.AddSingleton<IResettableTestState>(sp => sp.GetRequiredService<InMemoryCustomerNotifier>());
        });
    }
}
