using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

public sealed class DeliveryHandlerFactoryTests : BaseUnitTestCase<DeliveryHandlerFactory>
{
    private abstract record OrderEvent(string Id);
    private sealed record OrderPlaced(string Id) : OrderEvent(Id);
    private sealed record Ping(string Id);
    private sealed record Pong(string Id);

    private sealed class OrderConsumer : IConsumer<OrderEvent>
    {
        public Task ConsumeAsync(IMessageContext<OrderEvent> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class PingConsumer : IRequestConsumer<Ping, Pong>
    {
        public Task<Pong> ConsumeAsync(IMessageContext<Ping> messageContext, CancellationToken cancellationToken = default)
            => Task.FromResult(new Pong(messageContext.Message.Id));
    }

    [Fact]
    public void ForConsumerBuildsAConsumerHandlerWithTheRegistrationsRetryPolicy()
    {
        // Arrange
        var policy = new RetryPolicyDefinition { MaxRetryCount = 3 };

        // Act
        var handler = Target.ForConsumer(typeof(OrderConsumer), typeof(OrderEvent), policy);

        // Assert
        handler.Kind.ShouldBe(HandlerKind.Consumer);
        handler.Identity.ShouldBe($"{typeof(OrderConsumer).FullName}:{typeof(OrderEvent).FullName}");
        handler.RetryPolicy.ShouldBeSameAs(policy);
    }

    [Fact]
    public void ForRequestConsumerBuildsARequestConsumerHandlerWithoutARetryPolicy()
    {
        // Act
        var handler = Target.ForRequestConsumer(typeof(PingConsumer), typeof(Ping), typeof(Pong));

        // Assert
        handler.Kind.ShouldBe(HandlerKind.RequestConsumer);
        handler.Identity.ShouldBe($"{typeof(PingConsumer).FullName}:{typeof(Ping).FullName}");
        handler.RetryPolicy.ShouldBeNull();
    }

    [Fact]
    public void ForConsumerBuildsAFreshHandlerForEveryRegistrationOfTheSameShape()
    {
        // Arrange
        var firstPolicy = new RetryPolicyDefinition { MaxRetryCount = 1 };
        var secondPolicy = new RetryPolicyDefinition { MaxRetryCount = 2 };

        // Act
        var first = Target.ForConsumer(typeof(OrderConsumer), typeof(OrderEvent), firstPolicy);
        var second = Target.ForConsumer(typeof(OrderConsumer), typeof(OrderEvent), secondPolicy);

        // Assert
        first.ShouldNotBeSameAs(second);
        first.RetryPolicy.ShouldBeSameAs(firstPolicy);
        second.RetryPolicy.ShouldBeSameAs(secondPolicy);
    }

    [Fact]
    public void ForConsumerRejectsAConsumerThatDoesNotConsumeTheMessageType()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => Target.ForConsumer(typeof(OrderConsumer), typeof(Ping), retryPolicy: null));
    }

    [Fact]
    public void ForRequestConsumerRejectsAConsumerThatDoesNotProduceTheResponseType()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => Target.ForRequestConsumer(typeof(PingConsumer), typeof(Ping), typeof(OrderPlaced)));
    }
}
