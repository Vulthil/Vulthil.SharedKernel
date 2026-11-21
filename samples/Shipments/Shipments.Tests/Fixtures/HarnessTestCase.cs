using Microsoft.Extensions.DependencyInjection;
using Shipments.Messages;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.TestHarness;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Shipments.Tests.Fixtures;

public abstract class HarnessTestCase(HarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<Program>(factory, testOutputHelper), IClassFixture<HarnessWebApplicationFactory>
{
    protected ITestHarness Harness => ScopedServices.GetRequiredService<ITestHarness>();

    protected IPublisher Publisher => ScopedServices.GetRequiredService<IPublisher>();

    protected InMemoryCustomerNotifier CustomerNotifier => ScopedServices.GetRequiredService<InMemoryCustomerNotifier>();

    protected async Task SendCreateShipmentAsync(CreateShipment command, string? messageId = null)
    {
        var sendEndpoints = ScopedServices.GetRequiredService<ISendEndpointProvider>();
        var endpoint = await sendEndpoints.GetSendEndpointAsync(MessageAddress.Queue(QueueNames.ShipmentCommands), CancellationToken);

        await endpoint.SendAsync(command, context =>
        {
            context.AddHeader("source-service", "Shipments.Tests");
            if (messageId is not null)
            {
                context.SetMessageId(messageId);
            }

            return ValueTask.CompletedTask;
        }, CancellationToken);
    }
}
