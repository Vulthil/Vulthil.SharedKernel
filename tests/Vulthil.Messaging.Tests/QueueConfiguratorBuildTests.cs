using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class QueueConfiguratorBuildTests : BaseUnitTestCase<HostApplicationBuilder>
{
    protected override HostApplicationBuilder CreateInstance() => Host.CreateApplicationBuilder();

    private static QueueDefinition? GetQueue(HostApplicationBuilder builder, string queueName)
    {
        using var sp = builder.Services.BuildServiceProvider();
        return sp.GetRequiredService<IMessageConfigurationProvider>()
            .QueueDefinitions.FirstOrDefault(q => string.Equals(q.Name, queueName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildAutoSubscribesConcreteConsumerMessageTypes()
    {
        // Act
        Target.AddMessaging(m => m.ConfigureQueue("orders", q => q.AddConsumer<OrderPlacedConsumer>()));

        // Assert
        var queue = GetQueue(Target, "orders");
        queue.ShouldNotBeNull();
        queue.Subscriptions.ShouldContain(s => s.MessageType.Type == typeof(OrderPlaced));
    }

    [Fact]
    public void BuildThrowsWhenPolymorphicConsumerHasNoMatchingSubscription()
    {
        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() =>
            Target.AddMessaging(m => m.ConfigureQueue("orders", q => q.AddConsumer<PolymorphicOrderConsumer>())));

        ex.Message.ShouldContain("no concrete subscribed type");
        ex.Message.ShouldContain(nameof(PolymorphicOrderConsumer));
    }

    [Fact]
    public void BuildThrowsWhenSubscriptionHasNoMatchingConsumer()
    {
        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() =>
            Target.AddMessaging(m => m.ConfigureQueue("orders", q => q.Subscribe<OrderPlaced>())));

        ex.Message.ShouldContain("has no matching consumer");
        ex.Message.ShouldContain(typeof(OrderPlaced).FullName!);
    }

    [Fact]
    public void BuildAcceptsPolymorphicConsumerWithExplicitConcreteSubscription()
    {
        // Act
        Target.AddMessaging(m => m.ConfigureQueue("orders", q =>
        {
            q.Subscribe<OrderPlaced>();
            q.AddConsumer<PolymorphicOrderConsumer>();
        }));

        // Assert
        var queue = GetQueue(Target, "orders");
        queue.ShouldNotBeNull();
        queue.Subscriptions.ShouldContain(s => s.MessageType.Type == typeof(OrderPlaced));
        queue.Registrations.ShouldContain(r => r.ConsumerType.Type == typeof(PolymorphicOrderConsumer));
    }

    [Fact]
    public void SubscribeRejectsAbstractType()
    {
        var ex = Should.Throw<InvalidOperationException>(() =>
            Target.AddMessaging(m => m.ConfigureQueue("orders", q => q.Subscribe<AbstractOrderEventBase>())));

        ex.Message.ShouldContain("abstract or interface");
    }

    [Fact]
    public void SubscribeRejectsInterfaceType()
    {
        var ex = Should.Throw<InvalidOperationException>(() =>
            Target.AddMessaging(m => m.ConfigureQueue("orders", q => q.Subscribe<IOrderEvent>())));

        ex.Message.ShouldContain("abstract or interface");
    }

    [Fact]
    public void SubscribeAllDiscoversConcreteImplementersAndSkipsAbstractAndInterface()
    {
        // Act
        Target.AddMessaging(m => m.ConfigureQueue("orders", q =>
        {
            q.SubscribeAll<IOrderEvent>(typeof(IOrderEvent).Assembly);
            q.AddConsumer<PolymorphicOrderConsumer>();
        }));

        // Assert
        var queue = GetQueue(Target, "orders");
        queue.ShouldNotBeNull();
        queue.Subscriptions.ShouldContain(s => s.MessageType.Type == typeof(OrderPlaced));
        queue.Subscriptions.ShouldContain(s => s.MessageType.Type == typeof(OrderCancelled));
        queue.Subscriptions.ShouldNotContain(s => s.MessageType.Type == typeof(IOrderEvent));
        queue.Subscriptions.ShouldNotContain(s => s.MessageType.Type == typeof(IOrder));
        queue.Subscriptions.ShouldNotContain(s => s.MessageType.Type == typeof(AbstractOrderEventBase));
    }

    [Fact]
    public void BuildThrowsWhenRequestConsumerTargetsPolymorphicType()
    {
        var ex = Should.Throw<InvalidOperationException>(() =>
            Target.AddMessaging(m => m.ConfigureQueue("orders", q => q.AddRequestConsumer<PolymorphicRequestConsumer>())));

        ex.Message.ShouldContain("polymorphic request type");
    }

    [Fact]
    public void UseSingleActiveConsumerEnablesTheFlagOnTheQueue()
    {
        // Act
        Target.AddMessaging(m => m.ConfigureQueue("orders", q =>
        {
            q.UseSingleActiveConsumer();
            q.AddConsumer<OrderPlacedConsumer>();
        }));

        // Assert
        var queue = GetQueue(Target, "orders");
        queue.ShouldNotBeNull();
        queue.SingleActiveConsumer.ShouldBeTrue();
    }

    [Fact]
    public void QueueDoesNotEnableSingleActiveConsumerByDefault()
    {
        // Act
        Target.AddMessaging(m => m.ConfigureQueue("orders", q => q.AddConsumer<OrderPlacedConsumer>()));

        // Assert
        var queue = GetQueue(Target, "orders");
        queue.ShouldNotBeNull();
        queue.SingleActiveConsumer.ShouldBeFalse();
    }
}

internal interface IOrderEvent { }
internal interface IOrder : IOrderEvent { }
internal abstract record AbstractOrderEventBase : IOrderEvent;
internal sealed record OrderPlaced(string OrderId) : IOrder;
internal sealed record OrderCancelled(string OrderId) : IOrder;

internal sealed class OrderPlacedConsumer : IConsumer<OrderPlaced>
{
    public Task ConsumeAsync(IMessageContext<OrderPlaced> messageContext, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class PolymorphicOrderConsumer : IConsumer<IOrderEvent>
{
    public Task ConsumeAsync(IMessageContext<IOrderEvent> messageContext, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class PolymorphicRequestConsumer : IRequestConsumer<IOrderEvent, OrderPlaced>
{
    public Task<OrderPlaced> ConsumeAsync(IMessageContext<IOrderEvent> messageContext, CancellationToken cancellationToken = default)
        => Task.FromResult(new OrderPlaced("x"));
}
