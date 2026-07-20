using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Results;
using Vulthil.xUnit;

namespace Vulthil.Messaging.TestHarness.Tests;

public sealed class TestHarnessTests : BaseUnitTestCase
{
    private const int ResponderTemperature = 21;

    private readonly IHost _host;

    private ITestHarness Harness => _host.Services.GetRequiredService<ITestHarness>();
    private IPublisher Publisher => _host.Services.GetRequiredService<IPublisher>();
    private IRequester Requester => _host.Services.GetRequiredService<IRequester>();
    private ISendEndpointProvider SendEndpointProvider => _host.Services.GetRequiredService<ISendEndpointProvider>();

    public TestHarnessTests()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMessaging(messaging =>
        {
            messaging.ConfigureQueue("orders", queue => queue.AddConsumer<OrderCreatedConsumer>());
            messaging.ConfigureQueue("weather-commands", queue => queue.AddConsumer<RecordWeatherConsumer>());
            messaging.ConfigureQueue("weather-requests", queue => queue.AddRequestConsumer<GetWeatherConsumer>());
            messaging.ConfigureQueue("exploding", queue => queue.AddRequestConsumer<ExplodingConsumer>());
            messaging.ConfigureQueue("flaky", queue => queue.AddConsumer<FlakyConsumer>(c => c.UseRetry(r => r.Immediate(5))));
            messaging.ConfigureQueue("always-fails", queue => queue.AddConsumer<AlwaysFailsConsumer>(c => c.UseRetry(r => r.Immediate(3))));
            messaging.UseTestHarness();
        });
        builder.Services.AddSingleton<RetryProbe>();
        _host = builder.Build();
    }

    protected override ValueTask Dispose()
    {
        _host.Dispose();
        return base.Dispose();
    }

    [Fact]
    public async Task PublishingAnEventCapturesItAndRunsTheConsumerIncludingItsNestedPublish()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        // Act
        await Publisher.PublishAsync(new OrderCreated(orderId), CancellationToken);

        // Assert
        Harness.Published<OrderCreated>().ShouldHaveSingleItem().Message.Id.ShouldBe(orderId);
        Harness.Consumed<OrderCreated>().ShouldHaveSingleItem().Message.Id.ShouldBe(orderId);
        Harness.Published<OrderShipped>().ShouldHaveSingleItem().Message.Id.ShouldBe(orderId);
    }

    [Fact]
    public async Task SendingACommandCapturesItAndRunsTheConsumer()
    {
        // Arrange
        var command = new RecordWeather(Guid.NewGuid(), "Copenhagen");
        var endpoint = await SendEndpointProvider.GetSendEndpointAsync(new Uri("queue:weather-commands"), CancellationToken);

        // Act
        await endpoint.SendAsync(command, CancellationToken);

        // Assert
        Harness.Sent<RecordWeather>().ShouldHaveSingleItem().Message.City.ShouldBe("Copenhagen");
        Harness.Consumed<RecordWeather>().ShouldHaveSingleItem().Message.City.ShouldBe("Copenhagen");
    }

    [Fact]
    public async Task RequestingReturnsTheRegisteredRequestConsumersResponse()
    {
        // Arrange
        var request = new GetWeather("Oslo");

        // Act
        var result = await Requester.RequestAsync<GetWeather, WeatherForecast>(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.City.ShouldBe("Oslo");
        result.Value.TemperatureC.ShouldBe(GetWeatherConsumer.Temperature);
        Harness.Requested<GetWeather>().ShouldHaveSingleItem().Message.City.ShouldBe("Oslo");
        Harness.Consumed<GetWeather>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RequestingAFaultingConsumerReturnsAFailureResult()
    {
        // Arrange
        var request = new ExplodingRequest("boom");

        // Act
        var result = await Requester.RequestAsync<ExplodingRequest, WeatherForecast>(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("Messaging.Request.Failure");
        result.Error.Description.ShouldContain("boom");
        Harness.Consumed<ExplodingRequest>().ShouldBeEmpty();
    }

    [Fact]
    public async Task RequestingWithNoConsumerOrResponderTimesOut()
    {
        // Arrange
        var request = new ExternalQuery("unhandled");

        // Act
        var result = await Requester.RequestAsync<ExternalQuery, ExternalReply>(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("Messaging.Request.Timeout");
    }

    [Fact]
    public async Task RespondStubAnswersRequestsForAnOtherwiseUnhandledType()
    {
        // Arrange
        Harness.Respond<ExternalQuery, ExternalReply>(context => new ExternalReply($"mocked:{context.Message.Query}"));

        // Act
        var result = await Requester.RequestAsync<ExternalQuery, ExternalReply>(new ExternalQuery("ping"), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Answer.ShouldBe("mocked:ping");
    }

    [Fact]
    public async Task RespondStubTakesPrecedenceOverARegisteredRequestConsumer()
    {
        // Arrange
        Harness.Respond<GetWeather, WeatherForecast>(context => new WeatherForecast(context.Message.City, ResponderTemperature));

        // Act
        var result = await Requester.RequestAsync<GetWeather, WeatherForecast>(new GetWeather("Bergen"), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TemperatureC.ShouldBe(ResponderTemperature);
    }

    [Fact]
    public async Task HandleStubReactsToAPublishedMessage()
    {
        // Arrange
        var shippedIds = new List<Guid>();
        Harness.Handle<OrderShipped>(context =>
        {
            shippedIds.Add(context.Message.Id);
            return Task.CompletedTask;
        });
        var orderId = Guid.NewGuid();

        // Act — OrderCreatedConsumer publishes OrderShipped, which the stub observes.
        await Publisher.PublishAsync(new OrderCreated(orderId), CancellationToken);

        // Assert
        shippedIds.ShouldHaveSingleItem().ShouldBe(orderId);
    }

    [Fact]
    public async Task ConsumedContextSurfacesPublishedHeaderPrimitivesUnchanged()
    {
        // Arrange
        IReadOnlyDictionary<string, object?>? observed = null;
        Harness.Handle<OrderShipped>(context =>
        {
            observed = context.Headers;
            return Task.CompletedTask;
        });

        // Act
        await Publisher.PublishAsync(new OrderShipped(Guid.NewGuid()), ctx =>
        {
            ctx.AddHeader("tenant", "acme");
            ctx.AddHeader("attempt", 3);
            ctx.AddHeader("critical", true);
            return ValueTask.CompletedTask;
        }, CancellationToken);

        // Assert
        var headers = observed.ShouldNotBeNull();
        headers["tenant"].ShouldBeOfType<string>().ShouldBe("acme");
        headers["attempt"].ShouldBeOfType<int>().ShouldBe(3);
        headers["critical"].ShouldBeOfType<bool>().ShouldBe(true);
    }

    [Fact]
    public async Task ClearResetsCapturedMessages()
    {
        // Arrange
        await Publisher.PublishAsync(new OrderCreated(Guid.NewGuid()), CancellationToken);
        Harness.Published<OrderCreated>().ShouldNotBeEmpty();

        // Act
        Harness.Clear();

        // Assert
        Harness.Published<OrderCreated>().ShouldBeEmpty();
        Harness.Consumed<OrderCreated>().ShouldBeEmpty();
        Harness.Published<OrderShipped>().ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceTransportWithTestHarnessReplacesAnAlreadyRegisteredTransport()
    {
        // Arrange — a host composed with a stand-in "real" transport, as production code would register.
        var builder = Host.CreateApplicationBuilder();
        builder.AddMessaging(messaging => messaging.ConfigureQueue("orders", queue => queue.AddConsumer<OrderCreatedConsumer>()));
        builder.Services.AddSingleton<ITransport, ThrowingTransport>();
        builder.Services.AddSingleton<IPublisher, ThrowingPublisher>();
        builder.Services.AddSingleton<ISendEndpointProvider, ThrowingSendEndpointProvider>();
        builder.Services.AddSingleton<IRequester, ThrowingRequester>();

        // Act — swap the registered transport for the in-memory harness without touching the composition above.
        builder.Services.ReplaceTransportWithTestHarness();
        using var host = builder.Build();

        var orderId = Guid.NewGuid();
        await host.Services.GetRequiredService<IPublisher>().PublishAsync(new OrderCreated(orderId), CancellationToken);

        // Assert — the harness, not the stand-in transport, handled the publish.
        var harness = host.Services.GetRequiredService<ITestHarness>();
        harness.Published<OrderCreated>().ShouldHaveSingleItem().Message.Id.ShouldBe(orderId);
        harness.Consumed<OrderCreated>().ShouldHaveSingleItem().Message.Id.ShouldBe(orderId);
    }

    [Fact]
    public async Task OneWayConsumerThatKeepsFailingIsRetriedThenPublishesAFaultLeavingThePublishSuccessful()
    {
        // Arrange & Act — the consumer always throws, but the publish itself must still complete.
        await Publisher.PublishAsync(new AlwaysFailsMessage("boom"), CancellationToken);

        // Assert — never successfully consumed; exactly one Fault<T> published carrying the original message.
        Harness.Consumed<AlwaysFailsMessage>().ShouldBeEmpty();
        var fault = Harness.Published<Fault<AlwaysFailsMessage>>().ShouldHaveSingleItem();
        fault.Message.Message.Value.ShouldBe("boom");
        fault.Message.ExceptionMessage.ShouldContain("always fails");
    }

    [Fact]
    public async Task OneWayConsumerIsRetriedWithAnIncrementingRetryCountUntilItSucceeds()
    {
        // Arrange — fail on the first two attempts, succeed on the third.
        var probe = _host.Services.GetRequiredService<RetryProbe>();
        probe.SucceedOnRetry = 2;

        // Act
        await Publisher.PublishAsync(new FlakyMessage("ok"), CancellationToken);

        // Assert — three attempts with retry counts 0, 1, 2; consumed once on success; no fault.
        probe.ObservedRetryCounts.ToArray().ShouldBe([0, 1, 2]);
        Harness.Consumed<FlakyMessage>().ShouldHaveSingleItem();
        Harness.Published<Fault<FlakyMessage>>().ShouldBeEmpty();
    }

    private sealed class ThrowingTransport : ITransport
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingPublisher : IPublisher
    {
        public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken) where TMessage : notnull => throw new NotSupportedException();
        public Task PublishAsync<TMessage>(TMessage message, Func<IPublishContext, ValueTask>? configureContext = null, CancellationToken cancellationToken = default) where TMessage : notnull => throw new NotSupportedException();
    }

    private sealed class ThrowingSendEndpointProvider : ISendEndpointProvider
    {
        public ValueTask<ISendEndpoint> GetSendEndpointAsync(Uri address, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingRequester : IRequester
    {
        public Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(TRequest message, CancellationToken cancellationToken) where TRequest : notnull where TResponse : notnull => throw new NotSupportedException();
        public Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(TRequest message, Func<IRequestContext, ValueTask>? configureContext = null, CancellationToken cancellationToken = default) where TRequest : notnull where TResponse : notnull => throw new NotSupportedException();
    }

    public sealed record OrderCreated(Guid Id);
    public sealed record OrderShipped(Guid Id);
    public sealed record RecordWeather(Guid Id, string City);
    public sealed record GetWeather(string City);
    public sealed record WeatherForecast(string City, int TemperatureC);
    public sealed record ExplodingRequest(string Value);
    public sealed record ExternalQuery(string Query);
    public sealed record ExternalReply(string Answer);

    public sealed class OrderCreatedConsumer : IConsumer<OrderCreated>
    {
        public Task ConsumeAsync(IMessageContext<OrderCreated> messageContext, CancellationToken cancellationToken = default)
            => messageContext.PublishAsync(new OrderShipped(messageContext.Message.Id));
    }

    public sealed class RecordWeatherConsumer : IConsumer<RecordWeather>
    {
        public Task ConsumeAsync(IMessageContext<RecordWeather> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    public sealed class GetWeatherConsumer : IRequestConsumer<GetWeather, WeatherForecast>
    {
        public const int Temperature = 99;

        public Task<WeatherForecast> ConsumeAsync(IMessageContext<GetWeather> messageContext, CancellationToken cancellationToken = default)
            => Task.FromResult(new WeatherForecast(messageContext.Message.City, Temperature));
    }

    public sealed class ExplodingConsumer : IRequestConsumer<ExplodingRequest, WeatherForecast>
    {
        public Task<WeatherForecast> ConsumeAsync(IMessageContext<ExplodingRequest> messageContext, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(messageContext.Message.Value);
    }

    public sealed record FlakyMessage(string Value);
    public sealed record AlwaysFailsMessage(string Value);

    public sealed class RetryProbe
    {
        public ConcurrentQueue<int> ObservedRetryCounts { get; } = new();
        public int SucceedOnRetry { get; set; } = int.MaxValue;
    }

    public sealed class FlakyConsumer(RetryProbe probe) : IConsumer<FlakyMessage>
    {
        public Task ConsumeAsync(IMessageContext<FlakyMessage> messageContext, CancellationToken cancellationToken = default)
        {
            probe.ObservedRetryCounts.Enqueue(messageContext.RetryCount);
            return messageContext.RetryCount < probe.SucceedOnRetry
                ? throw new InvalidOperationException("flaky")
                : Task.CompletedTask;
        }
    }

    public sealed class AlwaysFailsConsumer : IConsumer<AlwaysFailsMessage>
    {
        public Task ConsumeAsync(IMessageContext<AlwaysFailsMessage> messageContext, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("always fails: " + messageContext.Message.Value);
    }
}
