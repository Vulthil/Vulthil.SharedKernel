using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.TestHarness.Tests;

public sealed class HarnessDuplicateRegistrationTests : BaseUnitTestCase
{
    private readonly IHost _host;
    private readonly Probe _probe = new();

    private ITestHarness Harness => _host.Services.GetRequiredService<ITestHarness>();
    private IPublisher Publisher => _host.Services.GetRequiredService<IPublisher>();

    public HarnessDuplicateRegistrationTests()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMessaging(messaging =>
        {
            messaging.ConfigureQueue("orders", queue =>
                queue.AddConsumer<OrderPlacedConsumer>(consumer => consumer.UseRetry(retry => retry.Immediate(2))));
            messaging.ConfigureQueue("orders", queue =>
                queue.AddConsumer<OrderPlacedConsumer>(consumer => consumer.UseRetry(retry => retry.Immediate(2))));
            messaging.UseTestHarness();
        });
        builder.Services.AddSingleton(_probe);
        _host = builder.Build();
    }

    protected override ValueTask Dispose()
    {
        _host.Dispose();
        return base.Dispose();
    }

    [Fact]
    public async Task AConsumerRegisteredTwiceWithEqualRetrySettingsRunsOncePerDelivery()
    {
        // Act
        await Publisher.PublishAsync(new OrderPlaced("A-1"), CancellationToken);

        // Assert
        _probe.Attempts.ShouldBe(1);
        Harness.Consumed<OrderPlaced>().ShouldHaveSingleItem();
    }

    public sealed record OrderPlaced(string OrderId);

    public sealed class Probe
    {
        public int Attempts { get; set; }
    }

    public sealed class OrderPlacedConsumer(Probe probe) : IConsumer<OrderPlaced>
    {
        public Task ConsumeAsync(IMessageContext<OrderPlaced> messageContext, CancellationToken cancellationToken = default)
        {
            probe.Attempts++;
            return Task.CompletedTask;
        }
    }
}
