using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

/// <summary>
/// Represents the ConsumerRegistrationTests.
/// </summary>
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

    /// <summary>
    /// Executes this member.
    /// </summary>
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

    /// <summary>
    /// Executes this member.
    /// </summary>
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

    /// <summary>
    /// Executes this member.
    /// </summary>
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

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void AddConsumerWithRoutingKeyShouldUseCustomRoutingKey()
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
                q.AddConsumer<TestMessageConsumer>(c =>
                {
                    c.Bind<TestMessage>(customRoutingKey);
                });
            });
        });

        // Assert
        var queue = GetQueueDefinitions(builder).First();
        queue.Registrations.First().RoutingKey.ShouldBe(customRoutingKey);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void AddConsumerWithoutRoutingKeyBindingShouldUseDefaultWildcard()
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
        queue.Registrations.First().RoutingKey.ShouldBe("#");
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
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

    /// <summary>
    /// Executes this member.
    /// </summary>
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
                q.AddConsumer<TestMessageConsumer>(c =>
                {
                    c.Bind<TestMessage>("route1");
                });
            });
            x.ConfigureQueue("Queue2", q =>
            {
                q.AddConsumer<TestMessageConsumer>(c =>
                {
                    c.Bind<TestMessage>("route2");
                });
            });
        });

        // Assert
        var queues = GetQueueDefinitions(builder);
        queues.Count.ShouldBe(2);

        var queue1 = queues.First(q => q.Name == "Queue1");
        queue1.Registrations.First().RoutingKey.ShouldBe("route1");

        var queue2 = queues.First(q => q.Name == "Queue2");
        queue2.Registrations.First().RoutingKey.ShouldBe("route2");
    }

    private class TestMessage
    {
        /// <summary>
        /// Gets or sets this member value.
        /// </summary>
        public string Content { get; set; } = string.Empty;
    }

    private class TestMessageConsumer : IConsumer<TestMessage>
    {
        /// <summary>
        /// Executes this member.
        /// </summary>
        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class AnotherMessage
    {
        /// <summary>
        /// Gets or sets this member value.
        /// </summary>
        public string Data { get; set; } = string.Empty;
    }

    private class AnotherTestConsumer : IConsumer<AnotherMessage>
    {
        /// <summary>
        /// Executes this member.
        /// </summary>
        public Task ConsumeAsync(IMessageContext<AnotherMessage> messageContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
