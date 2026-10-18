using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class MessagingOptionsTests : BaseUnitTestCase
{
    [Fact]
    public void MessagingOptionsShouldHaveDefaultTimeout()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void MessagingOptionsShouldHaveJsonSerializerOptions()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.JsonSerializerOptions.ShouldNotBeNull();
    }

    [Fact]
    public void SectionNameShouldBe()
    {
        // Arrange & Act & Assert
        MessagingOptions.SectionName.ShouldBe("Messaging:Options");
    }

    [Fact]
    public void RoutingKeyFormattersShouldBeEmpty()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.MessageConfigurations.Count.ShouldBe(0);
    }

    [Fact]
    public void CorrelationIdFormattersShouldBeEmpty()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        var config = options.GetMessageConfiguration(typeof(TestMessage));
        config.CorrelationIdFormatter.ShouldBeNull();
        config.RoutingKeyFormatter.ShouldBeNull();
        config.Exchange.ShouldBe(typeof(TestMessage).FullName!);
    }

    [Fact]
    public void DefaultTimeoutShouldBeModifiable()
    {
        // Arrange
        var options = new MessagingOptions();
        var newTimeout = TimeSpan.FromSeconds(60);

        // Act
        options.DefaultTimeout = newTimeout;

        // Assert
        options.DefaultTimeout.ShouldBe(newTimeout);
    }

    [Fact]
    public void JsonSerializerOptionsShouldBeModifiable()
    {
        // Arrange
        var options = new MessagingOptions();
        var newJsonOptions = new System.Text.Json.JsonSerializerOptions();

        // Act
        options.JsonSerializerOptions = newJsonOptions;

        // Assert
        options.JsonSerializerOptions.ShouldBe(newJsonOptions);
    }

    [Fact]
    public void GetMessageConfigurationShouldRegisterDistinctNewTypesUnderConcurrentFirstLookups()
    {
        // Arrange & Act & Assert
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var options = new MessagingOptions();
            Parallel.ForEach(ConcurrentMessageTypes, type => options.GetMessageConfiguration(type));
            AssertAllTypesRegistered(options);
        }
    }

    [Fact]
    public void GetMessageConfigurationShouldConvergeOnOneInstanceUnderConcurrentFirstLookupsOfTheSameType()
    {
        // Arrange & Act & Assert
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var options = new MessagingOptions();
            var configurations = new MessageConfiguration[16];
            Parallel.For(0, configurations.Length, slot => configurations[slot] = options.GetMessageConfiguration(typeof(Msg00)));
            configurations.ShouldAllBe(configuration => ReferenceEquals(configuration, configurations[0]));
        }
    }

    [Fact]
    public void ATypeThatDerivesFromAConfiguredBaseKeepsItsOwnUrnAndExchangeAndInheritsTheOtherSettings()
    {
        // Arrange
        var options = new MessagingOptions();
        var baseConfiguration = Configure<OrderEvent>(options, order =>
        {
            order.Exchange = "orders";
            order.Urn = new Uri("urn:message:Shop:OrderEvent");
            order.ExchangeType = MessagingExchangeType.Topic;
            order.Durable = false;
            order.AutoDelete = true;
            order.Arguments["x-queue-type"] = "quorum";
            order.UseRoutingKey(o => $"order.{o.Region}");
            order.UseCorrelationId(o => o.OrderId);
        });
        var placed = new OrderPlaced("o-1", "eu");

        // Act
        var configuration = options.GetMessageConfiguration(typeof(OrderPlaced));

        // Assert
        configuration.Urn.ShouldBe(new MessageConfiguration(typeof(OrderPlaced).FullName!).Urn);
        configuration.Exchange.ShouldBe(typeof(OrderPlaced).FullName);
        configuration.ExchangeType.ShouldBe(MessagingExchangeType.Topic);
        configuration.Durable.ShouldBeFalse();
        configuration.AutoDelete.ShouldBeTrue();
        configuration.Arguments["x-queue-type"].ShouldBe("quorum");
        configuration.Arguments.ShouldNotBeSameAs(baseConfiguration.Arguments);
        configuration.RoutingKeyFormatter.ShouldNotBeNull()(placed).ShouldBe("order.eu");
        configuration.CorrelationIdFormatter.ShouldNotBeNull()(placed).ShouldBe("o-1");
        options.GetMessageType(configuration.Urn).ShouldBe(typeof(OrderPlaced));
        options.GetMessageConfiguration(typeof(OrderEvent)).ShouldBeSameAs(baseConfiguration);
    }

    [Fact]
    public void SiblingTypesOfAConfiguredBaseKeepDistinctUrns()
    {
        // Arrange
        var options = new MessagingOptions();
        Configure<OrderEvent>(options, order => order.Exchange = "orders");

        // Act
        var placed = options.GetMessageConfiguration(typeof(OrderPlaced));
        var cancelled = options.GetMessageConfiguration(typeof(OrderCancelled));

        // Assert
        placed.Urn.ShouldNotBe(cancelled.Urn);
        options.GetMessageType(placed.Urn).ShouldBe(typeof(OrderPlaced));
        options.GetMessageType(cancelled.Urn).ShouldBe(typeof(OrderCancelled));
    }

    [Fact]
    public void ADerivedTypeTakesItsSettingsFromTheNearestConfiguredBase()
    {
        // Arrange
        var options = new MessagingOptions();
        Configure<OrderEvent>(options, order => order.Durable = false);
        Configure<ShippingEvent>(options, shipping => shipping.ExchangeType = MessagingExchangeType.Direct);

        // Act
        var configuration = options.GetMessageConfiguration(typeof(ParcelShipped));

        // Assert
        configuration.ExchangeType.ShouldBe(MessagingExchangeType.Direct);
        configuration.Durable.ShouldBeTrue();
        configuration.Exchange.ShouldBe(typeof(ParcelShipped).FullName);
    }

    [Fact]
    public void AUrnConflictFailsEveryLookupOfTheTypeNotOnlyTheFirst()
    {
        // Arrange
        var options = new MessagingOptions();
        Configure<UrnClaimer>(options, claimer => claimer.Urn = new MessageConfiguration(typeof(UrnVictim).FullName!).Urn);

        // Act
        var first = Should.Throw<InvalidOperationException>(() => options.GetMessageConfiguration(typeof(UrnVictim)));
        var second = Should.Throw<InvalidOperationException>(() => options.GetMessageConfiguration(typeof(UrnVictim)));

        // Assert
        second.Message.ShouldBe(first.Message);
    }

    private static MessageConfiguration<TMessage> Configure<TMessage>(MessagingOptions options, Action<MessageConfiguration<TMessage>> configure)
        where TMessage : notnull
    {
        var configuration = new MessageConfiguration<TMessage>();
        configure(configuration);
        options.MessageConfigurations[typeof(TMessage).FullName!] = configuration;
        options.RegisterMessageType(typeof(TMessage), configuration);
        return configuration;
    }

    private static void AssertAllTypesRegistered(MessagingOptions options)
    {
        foreach (var type in ConcurrentMessageTypes)
        {
            var configuration = options.GetMessageConfiguration(type);
            configuration.Exchange.ShouldBe(type.FullName);
            options.GetMessageType(configuration.Urn).ShouldBe(type);
        }
    }

    private static readonly Type[] ConcurrentMessageTypes =
    [
        typeof(Msg00), typeof(Msg01), typeof(Msg02), typeof(Msg03),
        typeof(Msg04), typeof(Msg05), typeof(Msg06), typeof(Msg07),
        typeof(Msg08), typeof(Msg09), typeof(Msg10), typeof(Msg11),
        typeof(Msg12), typeof(Msg13), typeof(Msg14), typeof(Msg15),
    ];

    private class TestMessage { }

    private abstract record OrderEvent(string OrderId, string Region);
    private sealed record OrderPlaced(string OrderId, string Region) : OrderEvent(OrderId, Region);
    private sealed record OrderCancelled(string OrderId, string Region) : OrderEvent(OrderId, Region);
    private abstract record ShippingEvent(string OrderId, string Region) : OrderEvent(OrderId, Region);
    private sealed record ParcelShipped(string OrderId, string Region) : ShippingEvent(OrderId, Region);
    private sealed record UrnClaimer(string Value);
    private sealed record UrnVictim(string Value);

    private sealed record Msg00(string Value);
    private sealed record Msg01(string Value);
    private sealed record Msg02(string Value);
    private sealed record Msg03(string Value);
    private sealed record Msg04(string Value);
    private sealed record Msg05(string Value);
    private sealed record Msg06(string Value);
    private sealed record Msg07(string Value);
    private sealed record Msg08(string Value);
    private sealed record Msg09(string Value);
    private sealed record Msg10(string Value);
    private sealed record Msg11(string Value);
    private sealed record Msg12(string Value);
    private sealed record Msg13(string Value);
    private sealed record Msg14(string Value);
    private sealed record Msg15(string Value);
}
