using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Queues;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

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
    public async Task StartingTheHostWithoutATransportThrowsAClearError()
    {
        // Arrange — messaging configured, but no transport (no UseRabbitMq/UseTestHarness).
        Target.AddMessaging(x => x.ConfigureQueue("orders", _ => { }));
        using var host = Target.Build();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync(CancellationToken));
        exception.Message.ShouldContain("No messaging transport is registered");
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
        def.RoutingKeyFormatter(testMessage).ShouldBe("route.test-123");
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

    [Fact]
    public void RegisteringTwoMessageTypesWithTheSameUrnThrows()
    {
        // Arrange
        var options = new MessagingOptions();
        var messagingConfigurator = new MessagingConfigurator(Target, options);
        messagingConfigurator.ConfigureMessage<UrnAlpha>(c => c.Urn = new Uri("urn:message:duplicate"));

        // Act
        var ex = Should.Throw<InvalidOperationException>(() =>
            messagingConfigurator.ConfigureMessage<UrnBeta>(c => c.Urn = new Uri("urn:message:duplicate")));

        // Assert
        ex.Message.ShouldContain("already registered");
        ex.Message.ShouldContain(typeof(UrnBeta).FullName!);
    }

    private sealed record TestMessage
    {
        public string Id { get; set; } = string.Empty;
    }

    private sealed record UrnAlpha(string Value);

    private sealed record UrnBeta(string Value);
}
