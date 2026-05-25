using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Queues;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

/// <summary>
/// Represents the MessagingConfiguratiorTests.
/// </summary>
public sealed class MessagingConfiguratiorTests : BaseUnitTestCase<HostApplicationBuilder>
{
    protected override HostApplicationBuilder CreateInstance() => Host.CreateApplicationBuilder();

    private static IReadOnlyCollection<QueueDefinition> GetQueueDefinitions(HostApplicationBuilder builder)
    {
        using var sp = builder.Services.BuildServiceProvider();
        return [.. sp.GetRequiredService<IMessageConfigurationProvider>().QueueDefinitions];
    }

    [Fact]
    public void AddMessagingShouldRegisterConsumerHostedService()
    {
        // Arrange

        // Act
        Target.AddMessaging(x => { });

        // Assert
        var hostedServices = Target.Services.Where(sd => sd.ImplementationType == typeof(ConsumerHostedService)).ToList();
        hostedServices.ShouldNotBeEmpty();
        hostedServices[0].Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddMessagingShouldRegisterMessageConfigurationProvider()
    {
        // Arrange

        // Act
        Target.AddMessaging(x => { });

        // Assert
        var messageConfigurationProviderService = Target.Services.Where(sd => sd.ServiceType == typeof(IMessageConfigurationProvider)).ToList();
        messageConfigurationProviderService.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void AddMessagingQueueShouldRegisterQueueDefinition()
    {
        // Arrange
        var queueName = "TestQueue";

        // Act
        Target.AddMessaging(x =>
        {
            x.ConfigureQueue(queueName, _ => { });
        });

        // Assert
        var queues = GetQueueDefinitions(Target);
        queues.ShouldNotBeEmpty();
    }

    [Fact]
    public void AddMessagingShouldThrowWhenQueueNameIsNull()
    {
        // Arrange

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
        {
            Target.AddMessaging(x =>
            {
                x.ConfigureQueue(null!, _ => { });
            });
        });
    }

    [Fact]
    public void AddMessagingShouldThrowWhenQueueNameIsEmpty()
    {
        // Arrange

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
        {
            Target.AddMessaging(x =>
            {
                x.ConfigureQueue(string.Empty, _ => { });
            });
        });
    }

    [Fact]
    public void AddMessagingMultipleQueuesShouldRegisterAll()
    {
        // Arrange

        // Act
        Target.AddMessaging(x =>
        {
            x.ConfigureQueue("Queue1", _ => { });
            x.ConfigureQueue("Queue2", _ => { });
            x.ConfigureQueue("Queue3", _ => { });
        });

        // Assert
        var queues = GetQueueDefinitions(Target);
        queues.Count.ShouldBe(3);
    }

    [Fact]
    public void RegisterRoutingKeyFormatterShouldStoreFormatterForType()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        var messagingConfigurator = new MessagingConfigurator(Target, options);
        messagingConfigurator.ConfigureMessage<TestMessage>(pd => pd.UseRoutingKey("test.route"));

        // Assert
        options.MessageConfigurations.ContainsKey(typeof(TestMessage).FullName!).ShouldBeTrue();
        var def = options.MessageConfigurations[typeof(TestMessage).FullName!];
        def.RoutingKeyFormatter
            .ShouldNotBeNull()
            .Invoke(It.IsAny<TestMessage>())
            .ShouldBe("test.route");
    }

    [Fact]
    public void RegisterRoutingKeyFormatterWithFuncShouldUseCustomLogic()
    {
        // Arrange
        var testMessage = new TestMessage { Id = "test-123" };
        var options = new MessagingOptions();

        // Act
        var messagingConfigurator = new MessagingConfigurator(Target, options);
        messagingConfigurator.ConfigureMessage<TestMessage>(pd => pd.UseRoutingKey((obj) => $"route.{obj.Id}"));

        // Assert
        var def = options.MessageConfigurations[typeof(TestMessage).FullName!];
        def.RoutingKeyFormatter.ShouldNotBeNull();
        def.RoutingKeyFormatter!(testMessage).ShouldBe("route.test-123");
    }

    [Fact]
    public void RegisterCorrelationIdFormatterShouldStoreFormatterForType()
    {
        // Arrange
        var testMessage = new TestMessage { Id = "correlation-456" };
        var options = new MessagingOptions();

        // Act
        var messagingConfigurator = new MessagingConfigurator(Target, options);
        messagingConfigurator.ConfigureMessage<TestMessage>(pd => pd.UseCorrelationId((obj) => obj.Id));

        // Assert
        options.MessageConfigurations.ContainsKey(typeof(TestMessage).FullName!).ShouldBeTrue();
        var def = options.MessageConfigurations[typeof(TestMessage).FullName!];
        def.CorrelationIdFormatter.ShouldNotBeNull()
            .Invoke(testMessage)
            .ShouldBe(testMessage.Id);
    }

    private sealed record TestMessage
    {
        public string Id { get; set; } = string.Empty;
    }
}
