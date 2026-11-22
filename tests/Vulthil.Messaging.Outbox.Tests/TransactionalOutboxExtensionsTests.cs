using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Outbox.Tests;

public sealed class TransactionalOutboxExtensionsTests : BaseUnitTestCase<HostApplicationBuilder>
{
    protected override HostApplicationBuilder CreateInstance() => Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

    [Fact]
    public void AddTransactionalConsumerRegistersTheConsumeFilterForTheMessageType()
    {
        // Act
        Target.AddMessaging(messaging => messaging.AddTransactionalConsumer<OrderPlaced>());

        // Assert
        Target.Services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IConsumeFilter<OrderPlaced>));
    }

    [Fact]
    public void AddTransactionalConsumerWithAConsumerTypeThrows()
    {
        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            Target.AddMessaging(messaging => messaging.AddTransactionalConsumer<OrderPlacedConsumer>()));

        // Assert
        exception.Message.ShouldContain(typeof(OrderPlacedConsumer).FullName!);
    }

    [Fact]
    public void AddTransactionalConsumerWithARequestConsumerTypeThrows()
    {
        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            Target.AddMessaging(messaging => messaging.AddTransactionalConsumer<OrderQuoteConsumer>()));

        // Assert
        exception.Message.ShouldContain(typeof(OrderQuoteConsumer).FullName!);
    }

    public sealed record OrderPlaced(string OrderId);

    public sealed class OrderPlacedConsumer : IConsumer<OrderPlaced>
    {
        public Task ConsumeAsync(IMessageContext<OrderPlaced> messageContext, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public sealed class OrderQuoteConsumer : IRequestConsumer<OrderPlaced, decimal>
    {
        public Task<decimal> ConsumeAsync(IMessageContext<OrderPlaced> messageContext, CancellationToken cancellationToken = default) => Task.FromResult(1m);
    }
}
