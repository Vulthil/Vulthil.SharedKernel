using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

public sealed class DeliveryDispatcherTests : BaseUnitTestCase<DeliveryDispatcher>
{
    private const string DeliveryMessageId = "message-1";
    private const string DeliveryCorrelationId = "order-42";
    private const string DeliveryRequestId = "request-7";
    private const string DeliveryRoutingKey = "orders.placed";

    private static readonly Uri DeliveryFaultAddress = new("queue:order-faults");
    private static readonly DeliveryHandlerFactory HandlerFactory = new();
    private static readonly OrderPlaced Order = new("order-1");
    private static readonly PriceRequest Request = new("sku-1");

    private readonly Probe _probe = new();
    private readonly CancellationTokenSource _delivery = new();
    private readonly ServiceProvider _services;

    private IMessageConfigurationProvider Provider => _services.GetRequiredService<IMessageConfigurationProvider>();

    public DeliveryDispatcherTests()
    {
        _services = new ServiceCollection()
            .AddSingleton(_probe)
            .AddSingleton<IMessageConfigurationProvider>(new MessagingOptions())
            .AddScoped<ScopeMarker>()
            .AddScoped<FirstConsumer>()
            .AddScoped<SecondConsumer>()
            .AddScoped<FilteredConsumer>()
            .AddScoped<PriceConsumer>()
            .AddScoped<FilteredPriceConsumer>()
            .AddScoped<IConsumeFilter<FilteredEvent>, ShortCircuitFilter<FilteredEvent>>()
            .AddScoped<IConsumeFilter<FilteredPriceRequest>, ShortCircuitFilter<FilteredPriceRequest>>()
            .BuildServiceProvider();
        Use(_services.GetRequiredService<IServiceScopeFactory>());
    }

    protected override async ValueTask Dispose()
    {
        _delivery.Dispose();
        await _services.DisposeAsync();
        await base.Dispose();
    }

    [Fact]
    public async Task ARoundRunsEveryHandlerAndTheNextRoundRunsOnlyTheHandlersThatFailed()
    {
        // Arrange
        _probe.On(FirstConsumer.Name, FailUntilRetry(1));
        var port = CreatePort();

        // Act
        var outcome = await Target.DispatchAsync([FirstHandler(Retry(2)), SecondHandler(Retry(2))], Order, port);

        // Assert
        _probe.Runs.ShouldBe(["first:0", "second:0", "first:1"]);
        outcome.Settlement.ShouldBe(DeliverySettlement.Acknowledge);
        outcome.Consumed.Select(handler => handler.Identity).ShouldBe([SecondHandler().Identity, FirstHandler().Identity]);
        port.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task EachHandlerRetriesUnderItsOwnPolicyAndAHandlerThatFailsForGoodEarlyPublishesItsFaultAtOnce()
    {
        // Arrange
        _probe.On(FirstConsumer.Name, AlwaysFail);
        _probe.On(SecondConsumer.Name, FailUntilRetry(2));
        var port = CreatePort();

        // Act
        var outcome = await Target.DispatchAsync([FirstHandler(Retry(1)), SecondHandler(Retry(3))], Order, port);

        // Assert
        _probe.Runs.ShouldBe(["first:0", "second:0", "first:1", "second:1", "second:2"]);
        outcome.Settlement.ShouldBe(DeliverySettlement.Acknowledge);
        outcome.Consumed.ShouldHaveSingleItem().Identity.ShouldBe(SecondHandler().Identity);
        outcome.Failures.Count.ShouldBe(4);
        port.Faults.ShouldHaveSingleItem().ShouldBeOfType<Fault<OrderPlaced>>().OriginalContext.RetryCount.ShouldBe(1);
    }

    [Fact]
    public async Task AHandlerThatSpendsItsRetriesPublishesOneFaultForItsLastRoundAndTheDeliveryIsDeadLettered()
    {
        // Arrange
        _probe.On(FirstConsumer.Name, AlwaysFail);
        var port = CreatePort();

        // Act
        var outcome = await Target.DispatchAsync([FirstHandler(Retry(2))], Order, port);

        // Assert
        _probe.Runs.ShouldBe(["first:0", "first:1", "first:2"]);
        outcome.Settlement.ShouldBe(DeliverySettlement.DeadLetter);
        outcome.Failures.Count.ShouldBe(3);
        outcome.Consumed.ShouldBeEmpty();
        port.Faults.ShouldHaveSingleItem().ShouldBeOfType<Fault<OrderPlaced>>().OriginalContext.RetryCount.ShouldBe(2);
    }

    [Fact]
    public async Task AHandlerWithoutARetryPolicyFailsForGoodOnItsFirstFailure()
    {
        // Arrange
        _probe.On(FirstConsumer.Name, AlwaysFail);
        var port = CreatePort();

        // Act
        var outcome = await Target.DispatchAsync([FirstHandler()], Order, port);

        // Assert
        _probe.Runs.ShouldBe(["first:0"]);
        outcome.Settlement.ShouldBe(DeliverySettlement.DeadLetter);
        port.Faults.ShouldHaveSingleItem();
        port.Waits.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnExceptionThePolicyIgnoresFailsTheHandlerForGoodOnItsFirstFailure()
    {
        // Arrange
        var policy = Retry(5);
        policy.IgnoreExceptions.Add(typeof(ArgumentException).FullName!);
        _probe.On(FirstConsumer.Name, _ => throw new ArgumentException("bad input"));
        var port = CreatePort();

        // Act
        var outcome = await Target.DispatchAsync([FirstHandler(policy)], Order, port);

        // Assert
        _probe.Runs.ShouldBe(["first:0"]);
        outcome.Settlement.ShouldBe(DeliverySettlement.DeadLetter);
        port.Faults.ShouldHaveSingleItem().ShouldBeOfType<Fault<OrderPlaced>>().OriginalContext.RetryCount.ShouldBe(0);
    }

    [Fact]
    public async Task AFaultCarriesTheMessageTheExceptionAndTheIdentityOfTheDelivery()
    {
        // Arrange
        _probe.On(FirstConsumer.Name, AlwaysFail);
        var port = CreatePort();

        // Act
        await Target.DispatchAsync([FirstHandler()], Order, port);

        // Assert
        var fault = port.Faults.ShouldHaveSingleItem().ShouldBeOfType<Fault<OrderPlaced>>();
        fault.Message.ShouldBe(Order);
        fault.ExceptionType.ShouldBe(typeof(InvalidOperationException).FullName);
        fault.ExceptionMessage.ShouldBe("always fails");
        fault.StackTrace.ShouldNotBeNullOrEmpty();
        fault.OriginalContext.MessageId.ShouldBe(DeliveryMessageId);
        fault.OriginalContext.CorrelationId.ShouldBe(DeliveryCorrelationId);
        fault.OriginalContext.FaultAddress.ShouldBe(DeliveryFaultAddress);
        fault.OriginalContext.RoutingKey.ShouldBe(DeliveryRoutingKey);
    }

    [Fact]
    public async Task WhenThePortCanRedeliverLaterTheFailedHandlersGoBackToTheBrokerForTheNextRound()
    {
        // Arrange
        _probe.On(FirstConsumer.Name, AlwaysFail);
        var port = CreatePort(canRedeliverLater: true);

        // Act
        var outcome = await Target.DispatchAsync([FirstHandler(Retry(3, TimeSpan.FromSeconds(2))), SecondHandler()], Order, port);

        // Assert
        _probe.Runs.ShouldBe(["first:0", "second:0"]);
        outcome.Settlement.ShouldBe(DeliverySettlement.RedeliverLater);
        outcome.RedeliveryHandlerIdentities.ShouldBe([FirstHandler().Identity]);
        outcome.RedeliveryRetryCount.ShouldBe(1);
        outcome.RedeliveryDelay.ShouldBe(TimeSpan.FromSeconds(2));
        outcome.Consumed.ShouldHaveSingleItem().Identity.ShouldBe(SecondHandler().Identity);
        port.Waits.ShouldBeEmpty();
        port.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnInMemoryPolicyRetriesInProcessEvenWhenThePortCanRedeliverLater()
    {
        // Arrange
        var policy = Retry(1, TimeSpan.FromSeconds(2));
        policy.InMemory = true;
        _probe.On(FirstConsumer.Name, FailUntilRetry(1));
        var port = CreatePort(canRedeliverLater: true);

        // Act
        var outcome = await Target.DispatchAsync([FirstHandler(policy)], Order, port);

        // Assert
        _probe.Runs.ShouldBe(["first:0", "first:1"]);
        port.Waits.ShouldBe([TimeSpan.FromSeconds(2)]);
        outcome.Settlement.ShouldBe(DeliverySettlement.Acknowledge);
    }

    [Fact]
    public async Task HandlersRetryingInTheSameRoundWaitOnceForTheLongestBackOffTheirPoliciesAskFor()
    {
        // Arrange
        _probe.On(FirstConsumer.Name, FailUntilRetry(1));
        _probe.On(SecondConsumer.Name, FailUntilRetry(1));
        var port = CreatePort();

        // Act
        await Target.DispatchAsync([FirstHandler(Retry(1, TimeSpan.FromSeconds(1))), SecondHandler(Retry(1, TimeSpan.FromSeconds(5)))], Order, port);

        // Assert
        port.Waits.ShouldBe([TimeSpan.FromSeconds(5)]);
        _probe.Runs.ShouldBe(["first:0", "second:0", "first:1", "second:1"]);
    }

    [Fact]
    public async Task ARedeliveryStartsAtTheRetryRoundThePortCarries()
    {
        // Arrange
        _probe.On(FirstConsumer.Name, AlwaysFail);
        var port = CreatePort(retryCount: 2);

        // Act
        var outcome = await Target.DispatchAsync([FirstHandler(Retry(3))], Order, port);

        // Assert
        _probe.Runs.ShouldBe(["first:2", "first:3"]);
        outcome.Settlement.ShouldBe(DeliverySettlement.DeadLetter);
        port.Faults.ShouldHaveSingleItem().ShouldBeOfType<Fault<OrderPlaced>>().OriginalContext.RetryCount.ShouldBe(3);
    }

    [Fact]
    public async Task AnOperationCanceledExceptionIsAnOrdinaryFailureWhileTheDeliveryGoesOn()
    {
        // Arrange
        _probe.On(FirstConsumer.Name, context => context.RetryCount == 0 ? throw new OperationCanceledException() : Task.CompletedTask);
        var port = CreatePort();

        // Act
        var outcome = await Target.DispatchAsync([FirstHandler(Retry(1))], Order, port);

        // Assert
        _probe.Runs.ShouldBe(["first:0", "first:1"]);
        outcome.Settlement.ShouldBe(DeliverySettlement.Acknowledge);
    }

    [Fact]
    public async Task AHandlerStoppedByTheEndOfTheDeliveryAbandonsItWithoutAFaultOrARetry()
    {
        // Arrange
        _probe.On(FirstConsumer.Name, EndTheDelivery);
        var port = CreatePort();

        // Act
        var outcome = await Target.DispatchAsync([FirstHandler(Retry(3)), SecondHandler()], Order, port);

        // Assert
        outcome.Settlement.ShouldBe(DeliverySettlement.Abandon);
        _probe.Runs.ShouldBe(["first:0"]);
        outcome.Failures.ShouldBeEmpty();
        port.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARetryWaitEndedByTheDeliveryTokenAbandonsTheDelivery()
    {
        // Arrange
        _probe.On(FirstConsumer.Name, AlwaysFail);
        var port = CreatePort(onWait: _delivery.Cancel);

        // Act
        var outcome = await Target.DispatchAsync([FirstHandler(Retry(3))], Order, port);

        // Assert
        outcome.Settlement.ShouldBe(DeliverySettlement.Abandon);
        _probe.Runs.ShouldBe(["first:0"]);
        port.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task EveryHandlerAttemptRunsInItsOwnServiceScope()
    {
        // Arrange
        _probe.On(FirstConsumer.Name, FailUntilRetry(1));
        var port = CreatePort();

        // Act
        await Target.DispatchAsync([FirstHandler(Retry(1)), SecondHandler()], Order, port);

        // Assert
        _probe.Scopes.Count.ShouldBe(3);
        _probe.Scopes.ShouldBeUnique();
    }

    [Fact]
    public async Task AOneWayDeliveryWhoseFilterShortCircuitsIsAcknowledgedWithoutAConsumedHandler()
    {
        // Arrange
        var port = CreatePort();

        // Act
        var outcome = await Target.DispatchAsync([FilteredHandler()], new FilteredEvent("event-1"), port);

        // Assert
        outcome.Settlement.ShouldBe(DeliverySettlement.Acknowledge);
        outcome.Consumed.ShouldBeEmpty();
        _probe.Runs.ShouldBeEmpty();
        port.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARequestConsumerRepliesWithItsResponseUnderTheIdsOfTheRequest()
    {
        // Arrange
        var port = CreatePort();

        // Act
        var outcome = await Target.DispatchAsync([PriceHandler()], Request, port);

        // Assert
        var reply = port.Replies.ShouldHaveSingleItem();
        reply.RequestId.ShouldBe(DeliveryRequestId);
        reply.CorrelationId.ShouldBe(DeliveryCorrelationId);
        RpcReply.ToResult<PriceQuote>(reply, Provider).Value.ShouldBe(new PriceQuote(Request.Sku, PriceConsumer.Price));
        outcome.Settlement.ShouldBe(DeliverySettlement.Acknowledge);
        outcome.Consumed.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AFailingRequestConsumerRepliesWithAnRpcFaultOnceAndPublishesNoFault()
    {
        // Arrange
        _probe.On(PriceConsumer.Name, AlwaysFail);
        var port = CreatePort();

        // Act
        var outcome = await Target.DispatchAsync([PriceHandler()], Request, port);

        // Assert
        _probe.Runs.ShouldBe(["price:0"]);
        RpcReply.ToResult<PriceQuote>(port.Replies.ShouldHaveSingleItem(), Provider).Error.Description.ShouldBe("always fails");
        outcome.Settlement.ShouldBe(DeliverySettlement.Acknowledge);
        outcome.Consumed.ShouldBeEmpty();
        outcome.Failures.ShouldBeEmpty();
        port.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARequestWhoseFilterShortCircuitsRepliesWithAShortCircuitFault()
    {
        // Arrange
        var port = CreatePort();

        // Act
        var outcome = await Target.DispatchAsync([FilteredPriceHandler()], new FilteredPriceRequest("sku-2"), port);

        // Assert
        RpcReply.ToResult<PriceQuote>(port.Replies.ShouldHaveSingleItem(), Provider).Error.Description.ShouldContain("did not produce a response");
        _probe.Runs.ShouldBeEmpty();
        outcome.Consumed.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARequestConsumerStoppedByTheEndOfTheDeliverySendsNoReply()
    {
        // Arrange
        _probe.On(PriceConsumer.Name, EndTheDelivery);
        var port = CreatePort();

        // Act
        var outcome = await Target.DispatchAsync([PriceHandler()], Request, port);

        // Assert
        outcome.Settlement.ShouldBe(DeliverySettlement.Abandon);
        port.Replies.ShouldBeEmpty();
    }

    [Fact]
    public void ARequestConsumersHandlerNeverCarriesARetryPolicy()
    {
        // Act
        var handler = HandlerFactory.ForRequestConsumer(typeof(PriceConsumer), typeof(PriceRequest), typeof(PriceQuote), Retry(3));

        // Assert
        handler.Kind.ShouldBe(HandlerKind.RequestConsumer);
        handler.RetryPolicy.ShouldBeNull();
    }

    [Fact]
    public void AHandlersIdentityJoinsTheConsumerAndMessageFullNames()
    {
        // Act
        var handler = FirstHandler();

        // Assert
        handler.Identity.ShouldBe($"{typeof(FirstConsumer).FullName}:{typeof(OrderPlaced).FullName}");
    }

    private static DeliveryHandler FirstHandler(RetryPolicyDefinition? retryPolicy = null)
        => HandlerFactory.ForConsumer(typeof(FirstConsumer), typeof(OrderPlaced), retryPolicy);

    private static DeliveryHandler SecondHandler(RetryPolicyDefinition? retryPolicy = null)
        => HandlerFactory.ForConsumer(typeof(SecondConsumer), typeof(OrderPlaced), retryPolicy);

    private static DeliveryHandler FilteredHandler()
        => HandlerFactory.ForConsumer(typeof(FilteredConsumer), typeof(FilteredEvent), retryPolicy: null);

    private static DeliveryHandler PriceHandler()
        => HandlerFactory.ForRequestConsumer(typeof(PriceConsumer), typeof(PriceRequest), typeof(PriceQuote), retryPolicy: null);

    private static DeliveryHandler FilteredPriceHandler()
        => HandlerFactory.ForRequestConsumer(typeof(FilteredPriceConsumer), typeof(FilteredPriceRequest), typeof(PriceQuote), retryPolicy: null);

    private static RetryPolicyDefinition Retry(int maxRetryCount, params TimeSpan[] intervals)
    {
        var policy = new RetryPolicyDefinition { MaxRetryCount = maxRetryCount };
        foreach (var interval in intervals)
        {
            policy.Intervals.Add(interval);
        }

        return policy;
    }

    private static Func<IMessageContext, Task> FailUntilRetry(int retryCount)
        => context => context.RetryCount < retryCount ? throw new InvalidOperationException("flaky") : Task.CompletedTask;

    private static Task AlwaysFail(IMessageContext context) => throw new InvalidOperationException("always fails");

    private async Task EndTheDelivery(IMessageContext context)
    {
        await _delivery.CancelAsync();
        throw new OperationCanceledException(_delivery.Token);
    }

    private FakePort CreatePort(int retryCount = 0, bool canRedeliverLater = false, Action? onWait = null)
        => new(_delivery.Token) { RetryCount = retryCount, CanRedeliverLater = canRedeliverLater, OnWait = onWait };

    public sealed record OrderPlaced(string Id);
    public sealed record FilteredEvent(string Id);
    public sealed record PriceRequest(string Sku);
    public sealed record FilteredPriceRequest(string Sku);
    public sealed record PriceQuote(string Sku, decimal Price);

    public sealed class ScopeMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public sealed class Probe
    {
        private readonly Dictionary<string, Func<IMessageContext, Task>> _behaviors = [];
        private readonly List<string> _runs = [];
        private readonly List<Guid> _scopes = [];

        public IReadOnlyList<string> Runs => _runs;

        public IReadOnlyList<Guid> Scopes => _scopes;

        public void On(string consumer, Func<IMessageContext, Task> behavior) => _behaviors[consumer] = behavior;

        public Task RunAsync(string consumer, IMessageContext context, ScopeMarker scope)
        {
            _runs.Add($"{consumer}:{context.RetryCount}");
            _scopes.Add(scope.Id);
            return _behaviors.TryGetValue(consumer, out var behavior) ? behavior(context) : Task.CompletedTask;
        }
    }

    public sealed class FirstConsumer(Probe probe, ScopeMarker scope) : IConsumer<OrderPlaced>
    {
        public const string Name = "first";

        public Task ConsumeAsync(IMessageContext<OrderPlaced> messageContext, CancellationToken cancellationToken = default)
            => probe.RunAsync(Name, messageContext, scope);
    }

    public sealed class SecondConsumer(Probe probe, ScopeMarker scope) : IConsumer<OrderPlaced>
    {
        public const string Name = "second";

        public Task ConsumeAsync(IMessageContext<OrderPlaced> messageContext, CancellationToken cancellationToken = default)
            => probe.RunAsync(Name, messageContext, scope);
    }

    public sealed class FilteredConsumer(Probe probe, ScopeMarker scope) : IConsumer<FilteredEvent>
    {
        public Task ConsumeAsync(IMessageContext<FilteredEvent> messageContext, CancellationToken cancellationToken = default)
            => probe.RunAsync("filtered", messageContext, scope);
    }

    public sealed class PriceConsumer(Probe probe, ScopeMarker scope) : IRequestConsumer<PriceRequest, PriceQuote>
    {
        public const string Name = "price";
        public const decimal Price = 9.5m;

        public async Task<PriceQuote> ConsumeAsync(IMessageContext<PriceRequest> messageContext, CancellationToken cancellationToken = default)
        {
            await probe.RunAsync(Name, messageContext, scope);
            return new PriceQuote(messageContext.Message.Sku, Price);
        }
    }

    public sealed class FilteredPriceConsumer(Probe probe, ScopeMarker scope) : IRequestConsumer<FilteredPriceRequest, PriceQuote>
    {
        public async Task<PriceQuote> ConsumeAsync(IMessageContext<FilteredPriceRequest> messageContext, CancellationToken cancellationToken = default)
        {
            await probe.RunAsync("filtered-price", messageContext, scope);
            return new PriceQuote(messageContext.Message.Sku, PriceConsumer.Price);
        }
    }

    public sealed class ShortCircuitFilter<TMessage> : IConsumeFilter<TMessage>
        where TMessage : notnull
    {
        public Task ConsumeAsync(IMessageContext<TMessage> context, ConsumeDelegate<TMessage> next) => Task.CompletedTask;
    }

    public sealed class FakePort(CancellationToken cancellationToken) : IDeliveryPort
    {
        private readonly List<TimeSpan> _waits = [];
        private readonly List<object> _faults = [];
        private readonly List<MessageEnvelope> _replies = [];

        public int RetryCount { get; init; }

        public CancellationToken CancellationToken => cancellationToken;

        public bool CanRedeliverLater { get; init; }

        public Action? OnWait { get; init; }

        public IReadOnlyList<TimeSpan> Waits => _waits;

        public IReadOnlyList<object> Faults => _faults;

        public IReadOnlyList<MessageEnvelope> Replies => _replies;

        public MessageContext<TMessage> CreateContext<TMessage>(TMessage message, IServiceProvider services, int retryCount)
            where TMessage : notnull
            => new()
            {
                Message = message,
                MessageId = DeliveryMessageId,
                CorrelationId = DeliveryCorrelationId,
                RequestId = DeliveryRequestId,
                FaultAddress = DeliveryFaultAddress,
                RoutingKey = DeliveryRoutingKey,
                Headers = new Dictionary<string, object?>(),
                RetryCount = retryCount,
                CancellationToken = cancellationToken,
            };

        public Task WaitBeforeRetryAsync(TimeSpan delay)
        {
            _waits.Add(delay);
            OnWait?.Invoke();
            return cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;
        }

        public Task PublishFaultAsync<TMessage>(Fault<TMessage> fault)
            where TMessage : notnull
        {
            _faults.Add(fault);
            return Task.CompletedTask;
        }

        public Task SendReplyAsync(MessageEnvelope reply)
        {
            _replies.Add(reply);
            return Task.CompletedTask;
        }
    }
}
