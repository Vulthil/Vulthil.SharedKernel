using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.TestHarness.Tests;

/// <summary>
/// Pins the harness's retry semantics to the transport's: the effective policy is the registration's own or
/// the queue default, polymorphic registrations keep their policy for concrete implementers, retries are per
/// handler, and request consumers never retry.
/// </summary>
public sealed class HarnessRetryPolicyResolutionTests : BaseUnitTestCase
{
    private readonly IHost _host;
    private readonly Probes _probes = new();

    private ITestHarness Harness => _host.Services.GetRequiredService<ITestHarness>();
    private IPublisher Publisher => _host.Services.GetRequiredService<IPublisher>();
    private IRequester Requester => _host.Services.GetRequiredService<IRequester>();

    public HarnessRetryPolicyResolutionTests()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMessaging(messaging =>
        {
            messaging.ConfigureQueue("queue-default-retry", queue =>
            {
                queue.UseRetry(retry => retry.Immediate(5));
                queue.AddConsumer<QueueDefaultFlakyConsumer>();
            });
            messaging.ConfigureQueue("poly-events", queue =>
            {
                queue.Subscribe<ConcreteShapeEvent>();
                queue.AddConsumer<PolymorphicShapeConsumer>(consumer => consumer.UseRetry(retry => retry.Immediate(5)));
            });
            messaging.ConfigureQueue("pair", queue =>
            {
                queue.AddConsumer<SteadyPairConsumer>();
                queue.AddConsumer<FlakyPairConsumer>(consumer => consumer.UseRetry(retry => retry.Immediate(2)));
            });
            messaging.ConfigureQueue("quotes", queue =>
            {
                queue.UseRetry(retry => retry.Immediate(3));
                queue.AddRequestConsumer<ExplodingQuoteConsumer>();
            });
            messaging.UseTestHarness();
        });
        builder.Services.AddSingleton(_probes);
        _host = builder.Build();
    }

    protected override ValueTask Dispose()
    {
        _host.Dispose();
        return base.Dispose();
    }

    [Fact]
    public async Task QueueLevelUseRetryAppliesToConsumersWithoutTheirOwnPolicy()
    {
        // Arrange
        _probes.QueueDefaultFailures = 2;

        // Act
        await Publisher.PublishAsync(new QueueDefaultMessage("ok"), CancellationToken);

        // Assert — three attempts under the queue default, consumed once, no fault.
        _probes.QueueDefaultAttempts.ShouldBe(3);
        Harness.Consumed<QueueDefaultMessage>().ShouldHaveSingleItem();
        Harness.Published<Fault<QueueDefaultMessage>>().ShouldBeEmpty();
    }

    [Fact]
    public async Task PolymorphicConsumersRetryPolicyAppliesToConcreteMessages()
    {
        // Arrange
        _probes.PolymorphicFailures = 2;

        // Act
        await Publisher.PublishAsync(new ConcreteShapeEvent("ok"), CancellationToken);

        // Assert
        _probes.PolymorphicAttempts.ShouldBe(3);
        Harness.Consumed<ConcreteShapeEvent>().ShouldHaveSingleItem();
        Harness.Published<Fault<ConcreteShapeEvent>>().ShouldBeEmpty();
    }

    [Fact]
    public async Task OnlyTheFailingConsumerIsRetriedWhenTwoShareAMessage()
    {
        // Arrange
        _probes.PairFailures = 1;

        // Act
        await Publisher.PublishAsync(new PairMessage("ok"), CancellationToken);

        // Assert — the steady consumer ran once; only the flaky one retried.
        _probes.SteadyPairAttempts.ShouldBe(1);
        _probes.FlakyPairAttempts.ShouldBe(2);
        Harness.Published<Fault<PairMessage>>().ShouldBeEmpty();
    }

    [Fact]
    public async Task RequestConsumersNeverRetryEvenWithAQueueLevelPolicy()
    {
        // Act
        var result = await Requester.RequestAsync<QuoteRequest, Quote>(new QuoteRequest("sku"), CancellationToken);

        // Assert — a single invocation whose exception became the fault reply; the queue policy did not re-run it.
        _probes.QuoteAttempts.ShouldBe(1);
        result.IsSuccess.ShouldBeFalse();
        result.Error.Description.ShouldContain("no quote");
    }

    public sealed record QueueDefaultMessage(string Value);
    public sealed record PairMessage(string Value);
    public sealed record QuoteRequest(string Sku);
    public sealed record Quote(string Sku, decimal Price);

    public interface IShapeEvent
    {
        string Value { get; }
    }

    public sealed record ConcreteShapeEvent(string Value) : IShapeEvent;

    public sealed class Probes
    {
        public int QueueDefaultAttempts { get; set; }
        public int QueueDefaultFailures { get; set; }
        public int PolymorphicAttempts { get; set; }
        public int PolymorphicFailures { get; set; }
        public int SteadyPairAttempts { get; set; }
        public int FlakyPairAttempts { get; set; }
        public int PairFailures { get; set; }
        public int QuoteAttempts { get; set; }
    }

    public sealed class QueueDefaultFlakyConsumer(Probes probes) : IConsumer<QueueDefaultMessage>
    {
        public Task ConsumeAsync(IMessageContext<QueueDefaultMessage> messageContext, CancellationToken cancellationToken = default)
            => ++probes.QueueDefaultAttempts <= probes.QueueDefaultFailures
                ? throw new InvalidOperationException("queue-default flaky")
                : Task.CompletedTask;
    }

    public sealed class PolymorphicShapeConsumer(Probes probes) : IConsumer<IShapeEvent>
    {
        public Task ConsumeAsync(IMessageContext<IShapeEvent> messageContext, CancellationToken cancellationToken = default)
            => ++probes.PolymorphicAttempts <= probes.PolymorphicFailures
                ? throw new InvalidOperationException("polymorphic flaky")
                : Task.CompletedTask;
    }

    public sealed class SteadyPairConsumer(Probes probes) : IConsumer<PairMessage>
    {
        public Task ConsumeAsync(IMessageContext<PairMessage> messageContext, CancellationToken cancellationToken = default)
        {
            probes.SteadyPairAttempts++;
            return Task.CompletedTask;
        }
    }

    public sealed class FlakyPairConsumer(Probes probes) : IConsumer<PairMessage>
    {
        public Task ConsumeAsync(IMessageContext<PairMessage> messageContext, CancellationToken cancellationToken = default)
            => ++probes.FlakyPairAttempts <= probes.PairFailures
                ? throw new InvalidOperationException("pair flaky")
                : Task.CompletedTask;
    }

    public sealed class ExplodingQuoteConsumer(Probes probes) : IRequestConsumer<QuoteRequest, Quote>
    {
        public Task<Quote> ConsumeAsync(IMessageContext<QuoteRequest> messageContext, CancellationToken cancellationToken = default)
        {
            probes.QuoteAttempts++;
            throw new InvalidOperationException("no quote");
        }
    }
}
