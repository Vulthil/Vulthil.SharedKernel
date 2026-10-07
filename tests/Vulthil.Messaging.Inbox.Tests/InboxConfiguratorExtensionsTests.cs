using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Inbox.Tests;

public sealed class InboxConfiguratorExtensionsTests : BaseUnitTestCase<HostApplicationBuilder>
{
    private readonly Mock<IMessageContext<TestMessage>> _context;

    public InboxConfiguratorExtensionsTests()
    {
        _context = GetMock<IMessageContext<TestMessage>>();
        _context.SetupGet(context => context.Message).Returns(new TestMessage("order-1", "customer-1"));
    }

    protected override HostApplicationBuilder CreateInstance() => Host.CreateApplicationBuilder();

    [Fact]
    public void WithoutAKeySelectorTheRegisteredSelectorDefersToTheMessageId()
    {
        // Arrange
        Action<IMessagingConfigurator> configure = messaging => messaging.AddIdempotentInbox<TestMessage>();

        // Act
        Target.AddMessaging(configure);

        // Assert
        ResolveKey().ShouldBeNull();
    }

    [Fact]
    public void APassedKeySelectorSuppliesTheKey()
    {
        // Arrange
        Action<IMessagingConfigurator> configure = messaging => messaging.AddIdempotentInbox<TestMessage>(OrderIdKey);

        // Act
        Target.AddMessaging(configure);

        // Assert
        ResolveKey().ShouldBe("order-1");
    }

    [Fact]
    public void CallingItTwiceWithoutAKeySelectorRegistersOneSelectorAndOneFilter()
    {
        // Arrange
        Action<IMessagingConfigurator> configure = messaging =>
        {
            messaging.AddIdempotentInbox<TestMessage>();
            messaging.AddIdempotentInbox<TestMessage>();
        };

        // Act
        Target.AddMessaging(configure);

        // Assert
        Target.Services.Count(descriptor => descriptor.ServiceType == typeof(IInboxKeySelector<TestMessage>)).ShouldBe(1);
        Target.Services.Count(descriptor => descriptor.ServiceType == typeof(IConsumeFilter<TestMessage>)).ShouldBe(1);
        ResolveKey().ShouldBeNull();
    }

    [Fact]
    public void CallingItTwiceWithTheSameMethodOnTheSameTargetKeepsThatKeySelector()
    {
        // Arrange
        var orders = new PrefixedOrderIdKey("order");
        Action<IMessagingConfigurator> configure = messaging =>
        {
            messaging.AddIdempotentInbox<TestMessage>(orders.KeyFor);
            messaging.AddIdempotentInbox<TestMessage>(orders.KeyFor);
        };

        // Act
        Target.AddMessaging(configure);

        // Assert
        Target.Services.Count(descriptor => descriptor.ServiceType == typeof(IInboxKeySelector<TestMessage>)).ShouldBe(1);
        ResolveKey().ShouldBe("order:order-1");
    }

    [Fact]
    public void ADifferentKeySelectorForTheSameMessageTypeThrows()
    {
        // Arrange
        Action<IMessagingConfigurator> configure = messaging =>
        {
            messaging.AddIdempotentInbox<TestMessage>(OrderIdKey);
            messaging.AddIdempotentInbox<TestMessage>(CustomerIdKey);
        };

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => Target.AddMessaging(configure));
        exception.Message.ShouldContain(typeof(TestMessage).FullName!);
    }

    [Fact]
    public void TheSameMethodOnADifferentTargetThrows()
    {
        // Arrange
        var orders = new PrefixedOrderIdKey("order");
        var invoices = new PrefixedOrderIdKey("invoice");
        Action<IMessagingConfigurator> configure = messaging =>
        {
            messaging.AddIdempotentInbox<TestMessage>(orders.KeyFor);
            messaging.AddIdempotentInbox<TestMessage>(invoices.KeyFor);
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => Target.AddMessaging(configure));
    }

    [Fact]
    public void AKeySelectorAfterACallWithoutOneThrows()
    {
        // Arrange
        Action<IMessagingConfigurator> configure = messaging =>
        {
            messaging.AddIdempotentInbox<TestMessage>();
            messaging.AddIdempotentInbox<TestMessage>(OrderIdKey);
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => Target.AddMessaging(configure));
    }

    [Fact]
    public void NoKeySelectorAfterACallWithOneThrows()
    {
        // Arrange
        Action<IMessagingConfigurator> configure = messaging =>
        {
            messaging.AddIdempotentInbox<TestMessage>(OrderIdKey);
            messaging.AddIdempotentInbox<TestMessage>();
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => Target.AddMessaging(configure));
    }

    [Fact]
    public void AnOwnKeySelectorRegisteredBeforeSuppliesTheKeyWhenNoKeySelectorIsPassed()
    {
        // Arrange
        Target.Services.AddSingleton<IInboxKeySelector<TestMessage>, CustomerIdKeySelector>();

        // Act
        Target.AddMessaging(messaging => messaging.AddIdempotentInbox<TestMessage>());

        // Assert
        ResolveKey().ShouldBe("customer-1");
    }

    [Fact]
    public void PassingAKeySelectorWhileAnOwnKeySelectorIsRegisteredThrows()
    {
        // Arrange
        Target.Services.AddSingleton<IInboxKeySelector<TestMessage>, CustomerIdKeySelector>();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => Target.AddMessaging(messaging => messaging.AddIdempotentInbox<TestMessage>(OrderIdKey)));
        exception.Message.ShouldContain(typeof(TestMessage).FullName!);
    }

    [Fact]
    public void AnOwnKeySelectorRegisteredAfterReplacesThePassedKeySelector()
    {
        // Arrange
        Target.AddMessaging(messaging => messaging.AddIdempotentInbox<TestMessage>(OrderIdKey));

        // Act
        Target.Services.AddSingleton<IInboxKeySelector<TestMessage>, CustomerIdKeySelector>();

        // Assert
        ResolveKey().ShouldBe("customer-1");
    }

    [Fact]
    public void PassingAKeySelectorAfterAnOwnKeySelectorReplacedItThrows()
    {
        // Arrange
        Action<IMessagingConfigurator> configure = messaging =>
        {
            messaging.AddIdempotentInbox<TestMessage>(OrderIdKey);
            messaging.HostApplicationBuilder.Services.AddSingleton<IInboxKeySelector<TestMessage>, CustomerIdKeySelector>();
            messaging.AddIdempotentInbox<TestMessage>(OrderIdKey);
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => Target.AddMessaging(configure));
    }

    [Fact]
    public void AKeyedKeySelectorRegistrationIsNotTheMessageTypeKeySelector()
    {
        // Arrange
        Target.Services.AddKeyedSingleton<IInboxKeySelector<TestMessage>, CustomerIdKeySelector>("tenant");

        // Act
        Target.AddMessaging(messaging => messaging.AddIdempotentInbox<TestMessage>(OrderIdKey));

        // Assert
        ResolveKey().ShouldBe("order-1");
    }

    [Fact]
    public void NullConfiguratorThrows()
    {
        // Arrange
        IMessagingConfigurator configurator = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => configurator.AddIdempotentInbox<TestMessage>());
    }

    private string? ResolveKey()
    {
        using var provider = Target.Services.BuildServiceProvider();
        return provider.GetRequiredService<IInboxKeySelector<TestMessage>>().GetKey(_context.Object);
    }

    private static string? OrderIdKey(IMessageContext<TestMessage> context) => context.Message.OrderId;

    private static string? CustomerIdKey(IMessageContext<TestMessage> context) => context.Message.CustomerId;

    public sealed record TestMessage(string OrderId, string CustomerId);

    private sealed class PrefixedOrderIdKey(string prefix)
    {
        public string? KeyFor(IMessageContext<TestMessage> context) => $"{prefix}:{context.Message.OrderId}";
    }

    private sealed class CustomerIdKeySelector : IInboxKeySelector<TestMessage>
    {
        public string? GetKey(IMessageContext<TestMessage> context) => context.Message.CustomerId;
    }
}
