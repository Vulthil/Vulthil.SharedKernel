using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.TestHarness.Tests;

/// <summary>
/// Pins the harness to the delivery rules it shares with the broker transport: retries run in rounds, a fault is
/// captured with the round it failed on but never reaches an <c>IConsumer&lt;Fault&lt;T&gt;&gt;</c>, and the
/// caller's token, not a consumer's own <see cref="OperationCanceledException"/>, ends a delivery.
/// </summary>
public sealed class HarnessDeliveryTests : BaseUnitTestCase
{
    private const string OrderCorrelationId = "order-42";

    private readonly IHost _host;
    private readonly DeliveryProbe _probe = new();

    private ITestHarness Harness => _host.Services.GetRequiredService<ITestHarness>();
    private IPublisher Publisher => _host.Services.GetRequiredService<IPublisher>();
    private IRequester Requester => _host.Services.GetRequiredService<IRequester>();

    public HarnessDeliveryTests()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMessaging(messaging =>
        {
            messaging.ConfigureQueue("rounds", queue =>
            {
                queue.AddConsumer<FlakyRoundConsumer>(consumer => consumer.UseRetry(retry => retry.Immediate(1)));
                queue.AddConsumer<SteadyRoundConsumer>();
            });
            messaging.ConfigureQueue("failing", queue => queue.AddConsumer<FailingConsumer>());
            messaging.ConfigureQueue("fault-observers", queue => queue.AddConsumer<FaultObserver>());
            messaging.ConfigureQueue("exhausting", queue => queue.AddConsumer<ExhaustingConsumer>(consumer => consumer.UseRetry(retry => retry.Immediate(2))));
            messaging.ConfigureQueue("ignoring", queue => queue.AddConsumer<IgnoringConsumer>(consumer => consumer.UseRetry(retry =>
            {
                retry.Immediate(5);
                retry.Ignore<ArgumentException>();
            })));
            messaging.ConfigureQueue("canceling", queue => queue.AddConsumer<CancelingConsumer>(consumer => consumer.UseRetry(retry => retry.Immediate(1))));
            messaging.ConfigureQueue("stopping", queue => queue.AddConsumer<StoppingConsumer>());
            messaging.ConfigureQueue("quotes", queue => queue.AddRequestConsumer<StoppingQuoteConsumer>());
            messaging.ConfigureQueue("dropped", queue => queue.AddConsumer<DroppedConsumer>());
            messaging.AddConsumeFilter<DroppingFilter>();
            messaging.UseTestHarness();
        });
        builder.Services.AddSingleton(_probe);
        _host = builder.Build();
    }

    protected override ValueTask Dispose()
    {
        _host.Dispose();
        _probe.Dispose();
        return base.Dispose();
    }

    [Fact]
    public async Task ConsumersOfOneMessageRetryInRoundsSoEveryConsumerRunsBeforeAnyRetry()
    {
        // Act
        await Publisher.PublishAsync(new RoundMessage("round"), CancellationToken);

        // Assert
        _probe.Runs.ShouldBe(["flaky:0", "steady:0", "flaky:1"]);
        Harness.Consumed<RoundMessage>().Count.ShouldBe(2);
        Harness.Published<Fault<RoundMessage>>().ShouldBeEmpty();
    }

    [Fact]
    public async Task AFaultIsCapturedButNeverDeliveredToAFaultConsumer()
    {
        // Act
        await Publisher.PublishAsync(new FailingMessage("boom"), CancellationToken);

        // Assert
        Harness.Published<Fault<FailingMessage>>().ShouldHaveSingleItem().Message.Message.Value.ShouldBe("boom");
        Harness.Consumed<Fault<FailingMessage>>().ShouldBeEmpty();
        _probe.Runs.ShouldNotContain("fault-observer:0");
    }

    [Fact]
    public async Task AFaultRunsTheHandleStubsRegisteredForIt()
    {
        // Arrange
        var observed = new List<string>();
        Harness.Handle<Fault<FailingMessage>>(context =>
        {
            observed.Add(context.Message.Message.Value);
            return Task.CompletedTask;
        });

        // Act
        await Publisher.PublishAsync(new FailingMessage("boom"), CancellationToken);

        // Assert
        observed.ShouldBe(["boom"]);
    }

    [Fact]
    public async Task AFaultCarriesTheRoundItFailedOnAndTheCorrelationIdOfTheDelivery()
    {
        // Act
        await Publisher.PublishAsync(new ExhaustingMessage("spent"), context =>
        {
            context.SetCorrelationId(OrderCorrelationId);
            return ValueTask.CompletedTask;
        }, CancellationToken);

        // Assert
        _probe.Runs.ShouldBe(["exhausting:0", "exhausting:1", "exhausting:2"]);
        var fault = Harness.Published<Fault<ExhaustingMessage>>().ShouldHaveSingleItem();
        fault.Message.OriginalContext.RetryCount.ShouldBe(2);
        fault.Message.OriginalContext.CorrelationId.ShouldBe(OrderCorrelationId);
        fault.Envelope.CorrelationId.ShouldBe(OrderCorrelationId);
    }

    [Fact]
    public async Task AnExceptionThePolicyIgnoresFaultsOnTheRoundThatThrewIt()
    {
        // Act
        await Publisher.PublishAsync(new IgnoredMessage("bad"), CancellationToken);

        // Assert
        _probe.Runs.ShouldBe(["ignoring:0"]);
        Harness.Published<Fault<IgnoredMessage>>().ShouldHaveSingleItem().Message.OriginalContext.RetryCount.ShouldBe(0);
    }

    [Fact]
    public async Task AConsumersOwnOperationCanceledExceptionIsRetriedLikeAnyOtherFailure()
    {
        // Act
        await Publisher.PublishAsync(new CancelingMessage("retry me"), CancellationToken);

        // Assert
        _probe.Runs.ShouldBe(["canceling:0", "canceling:1"]);
        Harness.Consumed<CancelingMessage>().ShouldHaveSingleItem();
        Harness.Published<Fault<CancelingMessage>>().ShouldBeEmpty();
    }

    [Fact]
    public async Task APublishWhoseTokenEndsTheDeliveryThrowsWithoutPublishingAFault()
    {
        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => Publisher.PublishAsync(new StoppingMessage("stop"), _probe.Delivery.Token));

        // Assert
        Harness.Consumed<StoppingMessage>().ShouldBeEmpty();
        Harness.Published<Fault<StoppingMessage>>().ShouldBeEmpty();
    }

    [Fact]
    public async Task ARequestWhoseTokenEndsTheDeliveryReturnsACancelledFailure()
    {
        // Act
        var result = await Requester.RequestAsync<QuoteRequest, Quote>(new QuoteRequest("sku-1"), _probe.Delivery.Token);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RequestErrorCodes.Cancelled);
        Harness.Consumed<QuoteRequest>().ShouldBeEmpty();
    }

    [Fact]
    public async Task AMessageAFilterDropsIsNotCapturedAsConsumed()
    {
        // Act
        await Publisher.PublishAsync(new DroppedMessage("drop"), CancellationToken);

        // Assert
        _probe.Runs.ShouldBeEmpty();
        Harness.Consumed<DroppedMessage>().ShouldBeEmpty();
        Harness.Published<Fault<DroppedMessage>>().ShouldBeEmpty();
    }

    public sealed record RoundMessage(string Value);
    public sealed record FailingMessage(string Value);
    public sealed record ExhaustingMessage(string Value);
    public sealed record IgnoredMessage(string Value);
    public sealed record CancelingMessage(string Value);
    public sealed record StoppingMessage(string Value);
    public sealed record QuoteRequest(string Sku);
    public sealed record Quote(string Sku, decimal Price);
    public sealed record DroppedMessage(string Value);

    public sealed class DeliveryProbe : IDisposable
    {
        private readonly List<string> _runs = [];

        public IReadOnlyList<string> Runs => _runs;

        public CancellationTokenSource Delivery { get; } = new();

        public void Record(string consumer, IMessageContext context) => _runs.Add($"{consumer}:{context.RetryCount}");

        public void Dispose() => Delivery.Dispose();
    }

    public sealed class FlakyRoundConsumer(DeliveryProbe probe) : IConsumer<RoundMessage>
    {
        public Task ConsumeAsync(IMessageContext<RoundMessage> messageContext, CancellationToken cancellationToken = default)
        {
            probe.Record("flaky", messageContext);
            return messageContext.RetryCount == 0 ? throw new InvalidOperationException("flaky") : Task.CompletedTask;
        }
    }

    public sealed class SteadyRoundConsumer(DeliveryProbe probe) : IConsumer<RoundMessage>
    {
        public Task ConsumeAsync(IMessageContext<RoundMessage> messageContext, CancellationToken cancellationToken = default)
        {
            probe.Record("steady", messageContext);
            return Task.CompletedTask;
        }
    }

    public sealed class FailingConsumer : IConsumer<FailingMessage>
    {
        public Task ConsumeAsync(IMessageContext<FailingMessage> messageContext, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(messageContext.Message.Value);
    }

    public sealed class FaultObserver(DeliveryProbe probe) : IConsumer<Fault<FailingMessage>>
    {
        public Task ConsumeAsync(IMessageContext<Fault<FailingMessage>> messageContext, CancellationToken cancellationToken = default)
        {
            probe.Record("fault-observer", messageContext);
            return Task.CompletedTask;
        }
    }

    public sealed class ExhaustingConsumer(DeliveryProbe probe) : IConsumer<ExhaustingMessage>
    {
        public Task ConsumeAsync(IMessageContext<ExhaustingMessage> messageContext, CancellationToken cancellationToken = default)
        {
            probe.Record("exhausting", messageContext);
            throw new InvalidOperationException("exhausting");
        }
    }

    public sealed class IgnoringConsumer(DeliveryProbe probe) : IConsumer<IgnoredMessage>
    {
        public Task ConsumeAsync(IMessageContext<IgnoredMessage> messageContext, CancellationToken cancellationToken = default)
        {
            probe.Record("ignoring", messageContext);
            throw new ArgumentException("ignored");
        }
    }

    public sealed class CancelingConsumer(DeliveryProbe probe) : IConsumer<CancelingMessage>
    {
        public Task ConsumeAsync(IMessageContext<CancelingMessage> messageContext, CancellationToken cancellationToken = default)
        {
            probe.Record("canceling", messageContext);
            return messageContext.RetryCount == 0 ? throw new OperationCanceledException("a timeout of its own") : Task.CompletedTask;
        }
    }

    public sealed class StoppingConsumer(DeliveryProbe probe) : IConsumer<StoppingMessage>
    {
        public async Task ConsumeAsync(IMessageContext<StoppingMessage> messageContext, CancellationToken cancellationToken = default)
        {
            await probe.Delivery.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public sealed class StoppingQuoteConsumer(DeliveryProbe probe) : IRequestConsumer<QuoteRequest, Quote>
    {
        public async Task<Quote> ConsumeAsync(IMessageContext<QuoteRequest> messageContext, CancellationToken cancellationToken = default)
        {
            await probe.Delivery.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return new Quote(messageContext.Message.Sku, 1m);
        }
    }

    public sealed class DroppedConsumer(DeliveryProbe probe) : IConsumer<DroppedMessage>
    {
        public Task ConsumeAsync(IMessageContext<DroppedMessage> messageContext, CancellationToken cancellationToken = default)
        {
            probe.Record("dropped", messageContext);
            return Task.CompletedTask;
        }
    }

    public sealed class DroppingFilter : IConsumeFilter<DroppedMessage>
    {
        public Task ConsumeAsync(IMessageContext<DroppedMessage> context, ConsumeDelegate<DroppedMessage> next) => Task.CompletedTask;
    }
}
