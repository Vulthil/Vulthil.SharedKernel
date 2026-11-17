using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.TestHarness.Tests;

/// <summary>
/// Pins the harness to the broker's routing: a send reaches only the queue that its address names, a publish reaches
/// every queue that consumes the message with a copy and retry rounds of its own, and a sent message keeps the address
/// it was sent to.
/// </summary>
public sealed class HarnessQueueRoutingTests : BaseUnitTestCase<IHost>
{
    private const string BillingQueue = "billing";
    private const string ShippingQueue = "shipping";

    private static readonly Uri BillingAddress = new($"queue:{BillingQueue}");
    private static readonly Uri ShippingAddress = new($"queue:{ShippingQueue}");
    private static readonly Uri ExternalAddress = new("queue:audit");

    private ITestHarness Harness => Target.Services.GetRequiredService<ITestHarness>();
    private IPublisher Publisher => Target.Services.GetRequiredService<IPublisher>();
    private ISendEndpointProvider SendEndpoints => Target.Services.GetRequiredService<ISendEndpointProvider>();
    private RoutingProbe Probe => Target.Services.GetRequiredService<RoutingProbe>();

    protected override IHost CreateInstance()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.AddMessaging(messaging =>
        {
            messaging.ConfigureQueue(BillingQueue, queue =>
            {
                queue.AddConsumer<BillingConsumer>();
                queue.AddConsumer<FlakyBillingConsumer>(consumer => consumer.UseRetry(retry => retry.Immediate(1)));
            });
            messaging.ConfigureQueue(ShippingQueue, queue =>
            {
                queue.AddConsumer<ShippingConsumer>();
                queue.AddConsumer<FlakyShippingConsumer>(consumer => consumer.UseRetry(retry => retry.Immediate(1)));
            });
            messaging.ConfigureQueue("orders", queue => queue.AddConsumer<OrderRouter>());
            messaging.UseTestHarness();
        });
        builder.Services.AddSingleton<RoutingProbe>();
        return builder.Build();
    }

    [Fact]
    public async Task ASendRunsOnlyTheConsumersOfTheQueueThatItsAddressNames()
    {
        // Arrange
        var endpoint = await SendEndpoints.GetSendEndpointAsync(BillingAddress, CancellationToken);

        // Act
        await endpoint.SendAsync(new OrderPlaced("A-1"), CancellationToken);

        // Assert
        Probe.Runs.ShouldBe([BillingQueue]);
        Harness.Consumed<OrderPlaced>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("rabbitmq://localhost/billing")]
    [InlineData("amqp://localhost/billing")]
    [InlineData("amqps://localhost/billing")]
    public async Task ASendToABrokerAddressReachesTheQueueThatItsPathNames(string address)
    {
        // Arrange
        var endpoint = await SendEndpoints.GetSendEndpointAsync(new Uri(address), CancellationToken);

        // Act
        await endpoint.SendAsync(new OrderPlaced("A-1"), CancellationToken);

        // Assert
        Probe.Runs.ShouldBe([BillingQueue]);
        Harness.Sent<OrderPlaced>().ShouldHaveSingleItem().DestinationAddress.ShouldBe(new Uri(address));
    }

    [Fact]
    public async Task ASendMatchesTheQueueNameCaseSensitively()
    {
        // Arrange
        var endpoint = await SendEndpoints.GetSendEndpointAsync(new Uri("queue:Billing"), CancellationToken);

        // Act
        await endpoint.SendAsync(new OrderPlaced("A-1"), CancellationToken);

        // Assert
        Probe.Runs.ShouldBeEmpty();
    }

    [Fact]
    public async Task ASendToAQueueThatTheAppDoesNotConfigureRunsNoConsumerAndIsCapturedWithItsAddress()
    {
        // Arrange
        var endpoint = await SendEndpoints.GetSendEndpointAsync(ExternalAddress, CancellationToken);

        // Act
        await endpoint.SendAsync(new OrderPlaced("A-1"), CancellationToken);

        // Assert
        Probe.Runs.ShouldBeEmpty();
        Harness.Consumed<OrderPlaced>().ShouldBeEmpty();
        Harness.Sent<OrderPlaced>().ShouldHaveSingleItem().DestinationAddress.ShouldBe(ExternalAddress);
    }

    [Fact]
    public async Task ASendToAQueueThatTheAppDoesNotConfigureStillRunsTheHandleStubs()
    {
        // Arrange
        var stubbed = new List<string>();
        Harness.Handle<OrderPlaced>(context =>
        {
            stubbed.Add(context.Message.OrderId);
            return Task.CompletedTask;
        });
        var endpoint = await SendEndpoints.GetSendEndpointAsync(ExternalAddress, CancellationToken);

        // Act
        await endpoint.SendAsync(new OrderPlaced("A-1"), CancellationToken);

        // Assert
        stubbed.ShouldBe(["A-1"]);
    }

    [Fact]
    public async Task ASendAddressThatNamesNoQueueIsRejected()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(
            async () => await SendEndpoints.GetSendEndpointAsync(new Uri("rabbitmq://localhost/"), CancellationToken));
    }

    [Fact]
    public async Task ASendFromAConsumerReachesOnlyTheQueueThatItsAddressNames()
    {
        // Act
        await Publisher.PublishAsync(new OrderAccepted("A-1"), CancellationToken);

        // Assert
        Probe.Runs.ShouldBe([ShippingQueue]);
        Harness.Sent<OrderPlaced>().ShouldHaveSingleItem().DestinationAddress.ShouldBe(ShippingAddress);
    }

    [Fact]
    public async Task OnlyTheSentCaptureOfASendRecordsTheDestinationAddress()
    {
        // Arrange
        var endpoint = await SendEndpoints.GetSendEndpointAsync(BillingAddress, CancellationToken);

        // Act
        await endpoint.SendAsync(new OrderPlaced("A-1"), CancellationToken);

        // Assert
        Harness.Sent<OrderPlaced>().ShouldHaveSingleItem().DestinationAddress.ShouldBe(BillingAddress);
        Harness.Consumed<OrderPlaced>().ShouldHaveSingleItem().DestinationAddress.ShouldBeNull();
    }

    [Fact]
    public async Task APublishedMessageRecordsNoDestinationAddress()
    {
        // Act
        await Publisher.PublishAsync(new OrderPlaced("A-1"), CancellationToken);

        // Assert
        Harness.Published<OrderPlaced>().ShouldHaveSingleItem().DestinationAddress.ShouldBeNull();
    }

    [Fact]
    public async Task APublishRunsTheConsumersOfEveryQueueThatConsumesTheMessage()
    {
        // Act
        await Publisher.PublishAsync(new OrderPlaced("A-1"), CancellationToken);

        // Assert
        Probe.Runs.ShouldBe([BillingQueue, ShippingQueue], ignoreOrder: true);
        Harness.Consumed<OrderPlaced>().Count.ShouldBe(2);
    }

    [Fact]
    public async Task APublishGivesEachQueueItsOwnCopyOfTheMessage()
    {
        // Act
        await Publisher.PublishAsync(new OrderPlaced("A-1"), CancellationToken);

        // Assert
        Probe.Messages.Count.ShouldBe(2);
        Probe.Messages[0].ShouldNotBeSameAs(Probe.Messages[1]);
    }

    [Fact]
    public async Task APublishRetriesTheConsumersOfEachQueueInRoundsOfTheirOwn()
    {
        // Act
        await Publisher.PublishAsync(new OrderFlagged("A-1"), CancellationToken);

        // Assert
        Probe.Runs.ShouldBe([$"{BillingQueue}:0", $"{BillingQueue}:1", $"{ShippingQueue}:0", $"{ShippingQueue}:1"]);
    }

    public sealed record OrderPlaced(string OrderId);
    public sealed record OrderFlagged(string OrderId);
    public sealed record OrderAccepted(string OrderId);

    public sealed class RoutingProbe
    {
        private readonly List<string> _runs = [];
        private readonly List<object> _messages = [];

        public IReadOnlyList<string> Runs => _runs;

        public IReadOnlyList<object> Messages => _messages;

        public void Record(string run, object message)
        {
            _runs.Add(run);
            _messages.Add(message);
        }
    }

    public sealed class BillingConsumer(RoutingProbe probe) : IConsumer<OrderPlaced>
    {
        public Task ConsumeAsync(IMessageContext<OrderPlaced> messageContext, CancellationToken cancellationToken = default)
        {
            probe.Record(BillingQueue, messageContext.Message);
            return Task.CompletedTask;
        }
    }

    public sealed class ShippingConsumer(RoutingProbe probe) : IConsumer<OrderPlaced>
    {
        public Task ConsumeAsync(IMessageContext<OrderPlaced> messageContext, CancellationToken cancellationToken = default)
        {
            probe.Record(ShippingQueue, messageContext.Message);
            return Task.CompletedTask;
        }
    }

    public sealed class FlakyBillingConsumer(RoutingProbe probe) : IConsumer<OrderFlagged>
    {
        public Task ConsumeAsync(IMessageContext<OrderFlagged> messageContext, CancellationToken cancellationToken = default)
        {
            probe.Record($"{BillingQueue}:{messageContext.RetryCount}", messageContext.Message);
            return messageContext.RetryCount == 0 ? throw new InvalidOperationException("flaky") : Task.CompletedTask;
        }
    }

    public sealed class FlakyShippingConsumer(RoutingProbe probe) : IConsumer<OrderFlagged>
    {
        public Task ConsumeAsync(IMessageContext<OrderFlagged> messageContext, CancellationToken cancellationToken = default)
        {
            probe.Record($"{ShippingQueue}:{messageContext.RetryCount}", messageContext.Message);
            return messageContext.RetryCount == 0 ? throw new InvalidOperationException("flaky") : Task.CompletedTask;
        }
    }

    public sealed class OrderRouter : IConsumer<OrderAccepted>
    {
        public Task ConsumeAsync(IMessageContext<OrderAccepted> messageContext, CancellationToken cancellationToken = default)
            => messageContext.SendAsync(ShippingAddress, new OrderPlaced(messageContext.Message.OrderId));
    }
}
