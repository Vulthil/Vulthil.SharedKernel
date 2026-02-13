using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
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
        options.RoutingKeyFormatters.Count.ShouldBe(0);
    }

    [Fact]
    public void CorrelationIdFormattersShouldBeEmpty()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.CorrelationIdFormatters.Count.ShouldBe(0);
    }

    [Fact]
    public void ReadOnlyRoutingKeyFormattersShouldReturnFormatters()
    {
        // Arrange
        var options = new MessagingOptions();
        options.RoutingKeyFormatters[typeof(TestMessage)] = msg => "test";

        // Act
        var readOnly = options.ReadOnlyRoutingKeyFormatters;

        // Assert
        readOnly.ShouldNotBeNull();
        readOnly.ContainsKey(typeof(TestMessage)).ShouldBeTrue();
    }

    [Fact]
    public void ReadOnlyCorrelationIdFormattersShouldReturnFormatters()
    {
        // Arrange
        var options = new MessagingOptions();
        options.CorrelationIdFormatters[typeof(TestMessage)] = msg => "correlation";

        // Act
        var readOnly = options.ReadOnlyCorrelationIdFormatters;

        // Assert
        readOnly.ShouldNotBeNull();
        readOnly.ContainsKey(typeof(TestMessage)).ShouldBeTrue();
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

    private class TestMessage { }
}
