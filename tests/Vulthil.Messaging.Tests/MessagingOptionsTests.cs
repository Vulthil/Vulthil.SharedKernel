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
