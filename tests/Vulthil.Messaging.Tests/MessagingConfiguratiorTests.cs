using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Queues;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class MessagingConfiguratiorTests : BaseUnitTestCase
{
    private static HostApplicationBuilder CreateHostBuilder()
    {
        return Host.CreateApplicationBuilder();
    }

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

    [Fact]
    public void AddMessagingQueueShouldRegisterQueueDefinition()
    {
        // Arrange
        var builder = CreateHostBuilder();
        var queueName = "TestQueue";

        // Act
        builder.AddMessaging(x =>
        {
            x.AddQueue(queueName, _ => { });
        });

        // Assert
        var queueServices = builder.Services.Where(sd => sd.ServiceType == typeof(QueueDefinition)).ToList();
        queueServices.ShouldNotBeEmpty();
        queueServices[0].ImplementationInstance.ShouldBeOfType<QueueDefinition>();
    }

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
                x.AddQueue(null!, _ => { });
            });
        });
    }

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
                x.AddQueue(string.Empty, _ => { });
            });
        });
    }

    [Fact]
    public void AddMessagingMultipleQueuesShouldRegisterAll()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act
        builder.AddMessaging(x =>
        {
            x.AddQueue("Queue1", _ => { });
            x.AddQueue("Queue2", _ => { });
            x.AddQueue("Queue3", _ => { });
        });

        // Assert
        var queueServices = builder.Services.Where(sd => sd.ServiceType == typeof(QueueDefinition)).ToList();
        queueServices.Count.ShouldBe(3);
    }

    [Fact]
    public void RegisterRoutingKeyFormatterShouldStoreFormatterForType()
    {
        // Arrange
        var testMessage = new TestMessage { Id = "123" };
        var options = new MessagingOptions();

        // Act
        var messagingConfigurator = new MessagingConfigurator(CreateHostBuilder(), options);
        messagingConfigurator.RegisterRoutingKeyFormatter<TestMessage>("test.route");

        // Assert
        options.RoutingKeyFormatters.ContainsKey(typeof(TestMessage)).ShouldBeTrue();
        var formatter = options.RoutingKeyFormatters[typeof(TestMessage)];
        formatter(testMessage).ShouldBe("test.route");
    }

    [Fact]
    public void RegisterRoutingKeyFormatterWithFuncShouldUseCustomLogic()
    {
        // Arrange
        var testMessage = new TestMessage { Id = "test-123" };
        var options = new MessagingOptions();

        // Act
        var messagingConfigurator = new MessagingConfigurator(CreateHostBuilder(), options);
        messagingConfigurator.RegisterRoutingKeyFormatter<TestMessage>(m => $"route.{m.Id}");

        // Assert
        var formatter = options.RoutingKeyFormatters[typeof(TestMessage)];
        formatter(testMessage).ShouldBe("route.test-123");
    }

    [Fact]
    public void RegisterCorrelationIdFormatterShouldStoreFormatterForType()
    {
        // Arrange
        var testMessage = new TestMessage { Id = "correlation-456" };
        var options = new MessagingOptions();

        // Act
        var messagingConfigurator = new MessagingConfigurator(CreateHostBuilder(), options);
        messagingConfigurator.RegisterCorrelationIdFormatter<TestMessage>(m => m.Id);

        // Assert
        options.CorrelationIdFormatters.ContainsKey(typeof(TestMessage)).ShouldBeTrue();
        var formatter = options.CorrelationIdFormatters[typeof(TestMessage)];
        formatter(testMessage).ShouldBe("correlation-456");
    }

    private class TestMessage
    {
        public string Id { get; set; } = string.Empty;
    }
}
