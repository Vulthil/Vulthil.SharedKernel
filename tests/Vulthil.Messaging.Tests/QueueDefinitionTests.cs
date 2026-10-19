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
        queue.Registrations.Count.ShouldBe(0);
    }

    [Fact]
    public void QueueDefinitionShouldAllowModifyingProperties()
    {
        // Arrange & Act
        var queue = new QueueDefinition("TestQueue")
        {
            Name = "NewQueueName",
            ConcurrencyLimit = 5,
            PrefetchCount = 10,
            IsQuorum = false,
            Durable = false,
            AutoDelete = true,
            Exclusive = true,
            ExchangeType = MessagingExchangeType.Direct,
            ExchangeDurable = false,
            ExchangeAutoDelete = true,
        };

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
        };

        // Act
        queue.AddConsumer(registration);

        // Assert
        queue.Registrations.Count.ShouldBe(1);
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
        registrations.Count.ShouldBe(1);
    }

    [Fact]
    public void AddConsumerIgnoresTheSameConsumerWithEqualRetrySettings()
    {
        // Arrange
        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(ConsumerRegistrationWith(ImmediateRetry(3)));

        // Act
        queue.AddConsumer(ConsumerRegistrationWith(ImmediateRetry(3)));

        // Assert
        queue.Registrations.ShouldHaveSingleItem();
    }

    [Fact]
    public void AddConsumerThrowsForTheSameConsumerWithADifferentRetryPolicy()
    {
        // Arrange
        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(ConsumerRegistrationWith(ImmediateRetry(3)));

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => queue.AddConsumer(ConsumerRegistrationWith(ImmediateRetry(5))));

        // Assert
        exception.Message.ShouldContain("TestQueue");
        exception.Message.ShouldContain(typeof(TestConsumer).FullName!);
        exception.Message.ShouldContain("different retry policy");
        queue.Registrations.ShouldHaveSingleItem();
    }

    [Fact]
    public void AddConsumerThrowsWhenOnlyTheNewRegistrationOfTheConsumerHasARetryPolicy()
    {
        // Arrange
        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(ConsumerRegistrationWith(retryPolicy: null));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => queue.AddConsumer(ConsumerRegistrationWith(ImmediateRetry(3))));
    }

    [Fact]
    public void AddConsumerAcceptsAnotherConsumerOfTheSameMessageWithItsOwnRetryPolicy()
    {
        // Arrange
        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(ConsumerRegistrationWith(ImmediateRetry(3)));

        // Act
        queue.AddConsumer(ConsumerRegistrationWith(ImmediateRetry(5), typeof(OtherTestConsumer)));

        // Assert
        queue.Registrations.Count.ShouldBe(2);
    }

    [Fact]
    public void UseDefaultRetryPolicyAcceptsAnEqualPolicyAgain()
    {
        // Arrange
        var queue = new QueueDefinition("TestQueue");
        queue.UseDefaultRetryPolicy(ImmediateRetry(3));

        // Act
        queue.UseDefaultRetryPolicy(ImmediateRetry(3));

        // Assert
        queue.DefaultRetryPolicy.ShouldBe(ImmediateRetry(3));
    }

    [Fact]
    public void UseDefaultRetryPolicyThrowsForADifferentPolicy()
    {
        // Arrange
        var queue = new QueueDefinition("TestQueue");
        queue.UseDefaultRetryPolicy(ImmediateRetry(3));

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => queue.UseDefaultRetryPolicy(ImmediateRetry(5)));

        // Assert
        exception.Message.ShouldContain("TestQueue");
        exception.Message.ShouldContain("different default retry policy");
        queue.DefaultRetryPolicy.ShouldBe(ImmediateRetry(3));
    }

    [Fact]
    public void UseDefaultRetryPolicyReplacesAPolicyThatCodeDidNotSet()
    {
        // Arrange
        var queue = new QueueDefinition("TestQueue") { DefaultRetryPolicy = ImmediateRetry(5) };

        // Act
        queue.UseDefaultRetryPolicy(ImmediateRetry(3));

        // Assert
        queue.DefaultRetryPolicy.ShouldBe(ImmediateRetry(3));
    }

    private static ConsumerRegistration ConsumerRegistrationWith(RetryPolicyDefinition? retryPolicy, Type? consumerType = null) => new()
    {
        ConsumerType = new ConsumerType(consumerType ?? typeof(TestConsumer)),
        MessageType = new MessageType(typeof(TestMessage)),
        RetryPolicy = retryPolicy,
    };

    private static RetryPolicyDefinition ImmediateRetry(int retryCount)
    {
        var configurator = new RetryPolicyConfigurator();
        configurator.Immediate(retryCount);
        return configurator.Build();
    }

    private class TestMessage { }
    private class TestConsumer : IConsumer<TestMessage>
    {
        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class OtherTestConsumer : IConsumer<TestMessage>
    {
        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
