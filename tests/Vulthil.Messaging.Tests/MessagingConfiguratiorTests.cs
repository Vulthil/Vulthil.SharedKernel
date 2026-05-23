using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Queues;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

/// <summary>
/// Represents the MessagingConfiguratiorTests.
/// </summary>
public sealed class MessagingConfiguratiorTests : BaseUnitTestCase
{
    private static HostApplicationBuilder CreateHostBuilder()
    {
        return Host.CreateApplicationBuilder();
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void AddMessagingShouldRegisterConsumerHostedService()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act
        builder.AddMessaging(x => { });

        // Assert
        var hostedServices = builder.Services.Where(sd => sd.ImplementationType == typeof(ConsumerHostedService)).ToList();
        hostedServices.ShouldNotBeEmpty();
        hostedServices[0].Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void AddMessagingShouldRegisterMessagingOptions()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act
        builder.AddMessaging(x => { });

        // Assert
        var optionsServices = builder.Services.Where(sd => sd.ServiceType == typeof(IOptions<MessagingOptions>)).ToList();
        optionsServices.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void AddMessagingQueueShouldRegisterQueueDefinition()
    {
        // Arrange
        var builder = CreateHostBuilder();
        var queueName = "TestQueue";

        // Act
        builder.AddMessaging(x =>
        {
            x.ConfigureQueue(queueName, _ => { });
        });

        // Assert
        var queueServices = builder.Services.Where(sd => sd.ServiceType == typeof(QueueDefinition)).ToList();
        queueServices.ShouldNotBeEmpty();
        queueServices[0].ImplementationInstance.ShouldBeOfType<QueueDefinition>();
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void AddMessagingShouldThrowWhenQueueNameIsNull()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
        {
            builder.AddMessaging(x =>
            {
                x.ConfigureQueue(null!, _ => { });
            });
        });
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void AddMessagingShouldThrowWhenQueueNameIsEmpty()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
        {
            builder.AddMessaging(x =>
            {
                x.ConfigureQueue(string.Empty, _ => { });
            });
        });
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void AddMessagingMultipleQueuesShouldRegisterAll()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act
        builder.AddMessaging(x =>
        {
            x.ConfigureQueue("Queue1", _ => { });
            x.ConfigureQueue("Queue2", _ => { });
            x.ConfigureQueue("Queue3", _ => { });
        });

        // Assert
        var queueServices = builder.Services.Where(sd => sd.ServiceType == typeof(QueueDefinition)).ToList();
        queueServices.Count.ShouldBe(3);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void RegisterRoutingKeyFormatterShouldStoreFormatterForType()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        var messagingConfigurator = new MessagingConfigurator(CreateHostBuilder(), options);
        messagingConfigurator.ConfigureMessage<TestMessage>(pd => pd.UseRoutingKey("test.route"));

        // Assert
        options.MessageConfigurations.ContainsKey(typeof(TestMessage)).ShouldBeTrue();
        var def = options.MessageConfigurations[typeof(TestMessage)];
        def.RoutingKeyFormatter
            .ShouldNotBeNull()
            .Invoke(It.IsAny<TestMessage>())
            .ShouldBe("test.route");
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void RegisterRoutingKeyFormatterWithFuncShouldUseCustomLogic()
    {
        // Arrange
        var testMessage = new TestMessage { Id = "test-123" };
        var options = new MessagingOptions();

        // Act
        var messagingConfigurator = new MessagingConfigurator(CreateHostBuilder(), options);
        messagingConfigurator.ConfigureMessage<TestMessage>(pd => pd.UseRoutingKey((obj) => $"route.{obj.Id}"));

        // Assert
        var def = options.MessageConfigurations[typeof(TestMessage)];
        def.RoutingKeyFormatter.ShouldNotBeNull();
        def.RoutingKeyFormatter!(testMessage).ShouldBe("route.test-123");
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void RegisterCorrelationIdFormatterShouldStoreFormatterForType()
    {
        // Arrange
        var testMessage = new TestMessage { Id = "correlation-456" };
        var options = new MessagingOptions();

        // Act
        var messagingConfigurator = new MessagingConfigurator(CreateHostBuilder(), options);
        messagingConfigurator.ConfigureMessage<TestMessage>(pd => pd.UseCorrelationId((obj) => obj.Id));

        // Assert
        options.MessageConfigurations.ContainsKey(typeof(TestMessage)).ShouldBeTrue();
        var def = options.MessageConfigurations[typeof(TestMessage)];
        def.CorrelationIdFormatter.ShouldNotBeNull()
            .Invoke(testMessage)
            .ShouldBe(testMessage.Id);
    }

    private class TestMessage
    {
        /// <summary>
        /// Gets or sets this member value.
        /// </summary>
        public string Id { get; set; } = string.Empty;
    }
}
