using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class ConsumerRegistrationTests : BaseUnitTestCase
{
    private static HostApplicationBuilder CreateHostBuilder()
    {
        return Host.CreateApplicationBuilder();
    }

    private static IReadOnlyCollection<QueueDefinition> GetQueueDefinitions(HostApplicationBuilder builder)
    {
        using var sp = builder.Services.BuildServiceProvider();
        return [.. sp.GetRequiredService<IMessageConfigurationProvider>().QueueDefinitions];
    }

    [Fact]
    public void AddConsumerShouldRegisterConsumerInServiceCollection()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act
        builder.AddMessaging(x =>
        {
            x.ConfigureQueue("TestQueue", q =>
            {
                q.AddConsumer<TestMessageConsumer>();
            });
        });

        // Assert
        var consumerServices = builder.Services.Where(sd => sd.ServiceType == typeof(TestMessageConsumer)).ToList();
        consumerServices.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void AddConsumerShouldRegisterConsumerOnlyOnce()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act
        builder.AddMessaging(x =>
        {
            x.ConfigureQueue("Queue1", q =>
            {
                q.AddConsumer<TestMessageConsumer>();
            });
            x.ConfigureQueue("Queue2", q =>
            {
                q.AddConsumer<TestMessageConsumer>();
            });
        });

        // Assert
        var registrations = builder.Services.Where(sd => sd.ServiceType == typeof(TestMessageConsumer)).ToList();
        registrations.Count.ShouldBe(1);
    }

    [Fact]
    public void AddConsumerShouldAddConsumerRegistrationToQueueDefinition()
    {
        // Arrange
        var builder = CreateHostBuilder();
        var queueName = "TestQueue";

        // Act
        builder.AddMessaging(x =>
        {
            x.ConfigureQueue(queueName, q =>
            {
                q.AddConsumer<TestMessageConsumer>();
            });
        });

        // Assert
        var queues = GetQueueDefinitions(builder);
        var queue = queues.First();
        queue.Name.ShouldBe(queueName);
        queue.Registrations.ShouldNotBeEmpty();
        queue.Registrations.First().ConsumerType.Type.ShouldBe(typeof(TestMessageConsumer));
        queue.Registrations.First().MessageType.Type.ShouldBe(typeof(TestMessage));
    }

    [Fact]
    public void SubscribeWithRoutingKeyShouldRecordOnSubscription()
    {
        // Arrange
        var builder = CreateHostBuilder();
        var queueName = "TestQueue";
        var customRoutingKey = "custom.routing.key";

        // Act
        builder.AddMessaging(x =>
        {
            x.ConfigureQueue(queueName, q =>
            {
                q.Subscribe<TestMessage>(customRoutingKey);
                q.AddConsumer<TestMessageConsumer>();
            });
        });

        // Assert
        var queue = GetQueueDefinitions(builder).First();
        queue.Subscriptions.ShouldContain(s =>
            s.MessageType.Type == typeof(TestMessage) && s.RoutingKey == customRoutingKey);
    }

    [Fact]
    public void AddConsumerShouldAutoSubscribeWithNullRoutingKey()
    {
        // Arrange
        var builder = CreateHostBuilder();
        var queueName = "TestQueue";

        // Act
        builder.AddMessaging(x =>
        {
            x.ConfigureQueue(queueName, q =>
            {
                q.AddConsumer<TestMessageConsumer>();
            });
        });

        // Assert
        var queue = GetQueueDefinitions(builder).First();
        queue.Subscriptions.ShouldContain(s =>
            s.MessageType.Type == typeof(TestMessage) && s.RoutingKey == null);
    }

    [Fact]
    public void AddMultipleConsumersToSameQueueShouldRegisterAll()
    {
        // Arrange
        var builder = CreateHostBuilder();
        var queueName = "TestQueue";

        // Act
        builder.AddMessaging(x =>
        {
            x.ConfigureQueue(queueName, q =>
            {
                q.AddConsumer<TestMessageConsumer>();
                q.AddConsumer<AnotherTestConsumer>();
            });
        });

        // Assert
        var queue = GetQueueDefinitions(builder).First();
        queue.Registrations.Count.ShouldBe(2);
        var types = queue.Registrations.Select(r => r.ConsumerType.Type).ToList();
        types.Contains(typeof(TestMessageConsumer)).ShouldBeTrue();
        types.Contains(typeof(AnotherTestConsumer)).ShouldBeTrue();
    }

    [Fact]
    public void SameConsumerInMultipleQueuesWithDifferentRoutingKeysShouldRegisterBoth()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act
        builder.AddMessaging(x =>
        {
            x.ConfigureQueue("Queue1", q =>
            {
                q.Subscribe<TestMessage>("route1");
                q.AddConsumer<TestMessageConsumer>();
            });
            x.ConfigureQueue("Queue2", q =>
            {
                q.Subscribe<TestMessage>("route2");
                q.AddConsumer<TestMessageConsumer>();
            });
        });

        // Assert
        var queues = GetQueueDefinitions(builder);
        queues.Count.ShouldBe(2);

        var queue1 = queues.First(q => q.Name == "Queue1");
        queue1.Subscriptions.ShouldContain(s => s.RoutingKey == "route1");

        var queue2 = queues.First(q => q.Name == "Queue2");
        queue2.Subscriptions.ShouldContain(s => s.RoutingKey == "route2");
    }

    private class TestMessage
    {
        public string Content { get; set; } = string.Empty;
    }

    private class TestMessageConsumer : IConsumer<TestMessage>
    {
        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class AnotherMessage
    {
        public string Data { get; set; } = string.Empty;
    }

    private class AnotherTestConsumer : IConsumer<AnotherMessage>
    {
        public Task ConsumeAsync(IMessageContext<AnotherMessage> messageContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
