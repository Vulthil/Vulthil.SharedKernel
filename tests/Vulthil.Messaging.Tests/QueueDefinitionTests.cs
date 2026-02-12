using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class QueueDefinitionTests : BaseUnitTestCase
{
    [Fact]
    public void MessageTypeShouldReturnFullName()
    {
        // Arrange
        var messageType = new MessageType(typeof(string));

        // Act
        var name = messageType.Name;

        // Assert
        name.ShouldBe(typeof(string).FullName);
    }

    [Fact]
    public void ConsumerTypeShouldReturnFullName()
    {
        // Arrange
        var consumerType = new ConsumerType(typeof(TestConsumer));

        // Act
        var name = consumerType.Name;

        // Assert
        name.ShouldContain(nameof(TestConsumer));
    }

    [Fact]
    public void QueueDefinitionShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var queue = new QueueDefinition("TestQueue");

        // Assert
        queue.Name.ShouldBe("TestQueue");
        queue.ConcurrencyLimit.Equals((ushort)1).ShouldBeTrue();
        queue.PrefetchCount.Equals((ushort)16).ShouldBeTrue();
        queue.IsQuorum.ShouldBeTrue();
        queue.Durable.ShouldBeTrue();
        queue.AutoDelete.ShouldBeFalse();
        queue.Exclusive.ShouldBeFalse();
        queue.ExchangeType.ShouldBe(MessagingExchangeType.Fanout);
        queue.ExchangeDurable.ShouldBeTrue();
        queue.ExchangeAutoDelete.ShouldBeFalse();
        queue.Registrations.Count().ShouldBe(0);
    }

    [Fact]
    public void QueueDefinitionShouldAllowModifyingProperties()
    {
        // Arrange
#pragma warning disable IDE0017
        var queue = new QueueDefinition("TestQueue")
        {
        };
#pragma warning restore IDE0017

        // Act
        queue.Name = "NewQueueName";
        queue.ConcurrencyLimit = 5;
        queue.PrefetchCount = 10;
        queue.IsQuorum = false;
        queue.Durable = false;
        queue.AutoDelete = true;
        queue.Exclusive = true;
        queue.ExchangeType = MessagingExchangeType.Direct;
        queue.ExchangeDurable = false;
        queue.ExchangeAutoDelete = true;

        // Assert
        queue.Name.ShouldBe("NewQueueName");
        queue.ConcurrencyLimit.Equals((ushort)5).ShouldBeTrue();
        queue.PrefetchCount.Equals((ushort)10).ShouldBeTrue();
        queue.IsQuorum.ShouldBeFalse();
        queue.Durable.ShouldBeFalse();
        queue.AutoDelete.ShouldBeTrue();
        queue.Exclusive.ShouldBeTrue();
        queue.ExchangeType.ShouldBe(MessagingExchangeType.Direct);
        queue.ExchangeDurable.ShouldBeFalse();
        queue.ExchangeAutoDelete.ShouldBeTrue();
    }

    [Fact]
    public void QueueDefinitionShouldTrackExchangeArguments()
    {
        // Arrange
        var queue = new QueueDefinition("TestQueue")
        {
        };

        // Act
        queue.ExchangeArguments["key1"] = "value1";
        queue.ExchangeArguments["key2"] = 42;

        // Assert
        queue.ExchangeArguments.Count.ShouldBe(2);
        queue.ExchangeArguments["key1"].ShouldBe("value1");
        queue.ExchangeArguments["key2"].ShouldBe(42);
    }

    [Fact]
    public void AddConsumerShouldRegisterInQueue()
    {
        // Arrange
        var queue = new QueueDefinition("TestQueue")
        {
        };
        var registration = new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(TestConsumer)),
            MessageType = new MessageType(typeof(TestMessage)),
            RoutingKey = "test.key"
        };

        // Act
        queue.AddConsumer(registration);

        // Assert
        queue.Registrations.Count().ShouldBe(1);
        queue.Registrations.First().ShouldBe(registration);
    }

    [Fact]
    public void RequestConsumerRegistrationShouldHaveResponseType()
    {
        // Arrange & Act
        var registration = new RequestConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(TestConsumer)),
            MessageType = new MessageType(typeof(TestMessage)),
            ResponseType = typeof(string)
        };

        // Assert
        registration.ResponseType.ShouldBe(typeof(string));
    }

    [Fact]
    public void ConsumerRegistrationDefaultRoutingKeyShouldBeWildcard()
    {
        // Arrange & Act
        var registration = new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(TestConsumer)),
            MessageType = new MessageType(typeof(TestMessage))
        };

        // Assert
        registration.RoutingKey.ShouldBe("#");
    }

    [Fact]
    public void RegistrationsShouldBeReadOnly()
    {
        // Arrange
        var queue = new QueueDefinition("TestQueue");
        var registration = new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(TestConsumer)),
            MessageType = new MessageType(typeof(TestMessage))
        };
        queue.AddConsumer(registration);

        // Act
        var registrations = queue.Registrations;

        // Assert
        registrations.ShouldNotBeNull();
        registrations.Count().ShouldBe(1);
    }

    private class TestMessage { }
    private class TestConsumer : IConsumer<TestMessage>
    {
        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
