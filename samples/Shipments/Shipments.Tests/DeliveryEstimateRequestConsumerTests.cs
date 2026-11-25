using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shipments.Application.Carrier;
using Shipments.Messages;
using Shouldly;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Shipments.Tests;

public sealed class DeliveryEstimateRequestConsumerTests : BaseUnitTestCase<DeliveryEstimateRequestConsumer>
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero));

    protected override ValueTask InitializeAsyncCore()
    {
        Use<TimeProvider>(_timeProvider);
        Use(Options.Create(new CarrierOptions { TransitTime = TimeSpan.FromDays(3) }));
        return base.InitializeAsyncCore();
    }

    [Fact]
    public async Task ConsumeAsync_estimates_delivery_after_the_transit_time()
    {
        // Arrange
        var context = GetMock<IMessageContext<GetDeliveryEstimate>>();
        context.SetupGet(c => c.Message).Returns(new GetDeliveryEstimate("Ada"));

        // Act
        var estimate = await Target.ConsumeAsync(context.Object, CancellationToken);

        // Assert
        estimate.EstimatedDeliveryUtc.ShouldBe(_timeProvider.GetUtcNow().AddDays(3));
    }
}
