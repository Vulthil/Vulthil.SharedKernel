using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

public sealed class MessageHandlerFactoryTests : BaseUnitTestCase<MessageHandlerFactoryTests.RecordingHandlerFactory>
{
    /// <summary>A handler stand-in that records the type arguments and policy the factory bound it with.</summary>
    public sealed record BuiltHandler(Type ConsumerType, Type MessageType, Type? ResponseType, RetryPolicyDefinition? RetryPolicy);

    /// <summary>Exercises the base class through its two generic overrides, recording what each was bound with.</summary>
    public sealed class RecordingHandlerFactory : MessageHandlerFactory<BuiltHandler>
    {
        protected override BuiltHandler CreateConsumerHandler<TConsumer, TMessage>(RetryPolicyDefinition? retryPolicy)
            => new(typeof(TConsumer), typeof(TMessage), ResponseType: null, retryPolicy);

        protected override BuiltHandler CreateRequestConsumerHandler<TConsumer, TRequest, TResponse>(RetryPolicyDefinition? retryPolicy)
            => new(typeof(TConsumer), typeof(TRequest), typeof(TResponse), retryPolicy);
    }

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
    public void ForConsumerBindsTheRegistrationTypesAndPassesTheRetryPolicy()
    {
        // Arrange
        var policy = new RetryPolicyDefinition { MaxRetryCount = 3 };

        // Act
        var handler = Target.ForConsumer(typeof(OrderConsumer), typeof(OrderEvent), policy);

        // Assert
        handler.ConsumerType.ShouldBe(typeof(OrderConsumer));
        handler.MessageType.ShouldBe(typeof(OrderEvent));
        handler.ResponseType.ShouldBeNull();
        handler.RetryPolicy.ShouldBeSameAs(policy);
    }

    [Fact]
    public void ForRequestConsumerBindsTheRequestAndResponseTypes()
    {
        // Act
        var handler = Target.ForRequestConsumer(typeof(PingConsumer), typeof(Ping), typeof(Pong), retryPolicy: null);

        // Assert
        handler.ConsumerType.ShouldBe(typeof(PingConsumer));
        handler.MessageType.ShouldBe(typeof(Ping));
        handler.ResponseType.ShouldBe(typeof(Pong));
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
        Should.Throw<ArgumentException>(() => Target.ForRequestConsumer(typeof(PingConsumer), typeof(Ping), typeof(OrderPlaced), retryPolicy: null));
    }
}
