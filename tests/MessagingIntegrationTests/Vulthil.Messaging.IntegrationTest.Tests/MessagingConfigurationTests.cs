using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using RabbitMQ.Client;
using Vulthil.Extensions.Testing;
using Vulthil.Messaging.IntegrationTest.Contracts;
using Vulthil.Results;

namespace Vulthil.Messaging.IntegrationTest.Tests;

[Collection(nameof(AppHostCollection))]
public sealed class MessagingConfigurationTests(AppHostFixture fixture)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task PublishingAnEventDeliversItToTheConsumer()
    {
        var @event = new WeatherUpdatedEvent(Guid.NewGuid(), "Copenhagen", 18);

        using var publishResponse = await fixture.ProducerClient.PostAsJsonAsync(
            "/api/publish-event",
            @event,
            TestContext.Current.CancellationToken);
        publishResponse.IsSuccessStatusCode.ShouldBeTrue();

        var pollResult = await Polling.WaitAsync(
            PollTimeout,
            ct => TryFindMatchAsync<WeatherUpdatedEvent>(fixture.ConsumerClient, "/api/received/events", e => e.Id == @event.Id, ct),
            PollInterval,
            TestContext.Current.CancellationToken);

        pollResult.IsSuccess.ShouldBeTrue();
        pollResult.Value.Location.ShouldBe(@event.Location);
        pollResult.Value.TemperatureC.ShouldBe(@event.TemperatureC);
    }

    [Fact]
    public async Task SendingACommandDeliversItToTheConsumer()
    {
        var command = new RecordWeatherCommand(Guid.NewGuid(), "Oslo", -3);

        using var sendResponse = await fixture.ProducerClient.PostAsJsonAsync(
            "/api/send-command",
            command,
            TestContext.Current.CancellationToken);
        sendResponse.IsSuccessStatusCode.ShouldBeTrue();

        var pollResult = await Polling.WaitAsync(
            PollTimeout,
            ct => TryFindMatchAsync<RecordWeatherCommand>(fixture.ConsumerClient, "/api/received/commands", c => c.Id == command.Id, ct),
            PollInterval,
            TestContext.Current.CancellationToken);

        pollResult.IsSuccess.ShouldBeTrue();
        pollResult.Value.Location.ShouldBe(command.Location);
        pollResult.Value.TemperatureC.ShouldBe(command.TemperatureC);
    }

    [Fact]
    public async Task SendingARequestReceivesAReplyFromTheConsumer()
    {
        var request = new GetWeatherRequest("Stockholm");

        using var requestResponse = await fixture.ProducerClient.PostAsJsonAsync(
            "/api/request",
            request,
            TestContext.Current.CancellationToken);
        requestResponse.IsSuccessStatusCode.ShouldBeTrue();

        var response = await requestResponse.Content.ReadFromJsonAsync<GetWeatherResponse>(TestContext.Current.CancellationToken);
        response.ShouldNotBeNull();
        response.Location.ShouldBe(request.Location);
    }

    [Fact]
    public async Task PublishingAStockChangedEventFiresPolymorphicAndConcreteConsumers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var @event = new StockChangedEvent(Guid.NewGuid(), "SKU-123", -5);

        using var publishResponse = await fixture.ProducerClient.PostAsJsonAsync(
            "/api/publish-inventory",
            @event,
            cancellationToken);
        publishResponse.IsSuccessStatusCode.ShouldBeTrue();

        var polymorphicResult = await Polling.WaitAsync(
            PollTimeout,
            ct => TryFindMatchAsync<StockChangedEvent>(fixture.ConsumerClient, "/api/received/inventory.any", e => e.Id == @event.Id, ct),
            PollInterval,
            cancellationToken);

        var concreteResult = await Polling.WaitAsync(
            PollTimeout,
            ct => TryFindMatchAsync<StockChangedEvent>(fixture.ConsumerClient, "/api/received/inventory.stock-changed", e => e.Id == @event.Id, ct),
            PollInterval,
            cancellationToken);

        polymorphicResult.IsSuccess.ShouldBeTrue();
        concreteResult.IsSuccess.ShouldBeTrue();
        concreteResult.Value.Sku.ShouldBe(@event.Sku);
    }

    [Fact]
    public async Task SendingACommandForwardsAnAuditEntryViaSendAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new RecordWeatherCommand(Guid.NewGuid(), "Bergen", 7);

        using var sendResponse = await fixture.ProducerClient.PostAsJsonAsync(
            "/api/send-command",
            command,
            cancellationToken);
        sendResponse.IsSuccessStatusCode.ShouldBeTrue();

        var auditResult = await Polling.WaitAsync(
            PollTimeout,
            ct => TryFindMatchAsync<WeatherAuditEntry>(fixture.ConsumerClient, "/api/received/audit", a => a.SourceId == command.Id, ct),
            PollInterval,
            cancellationToken);

        auditResult.IsSuccess.ShouldBeTrue();
        auditResult.Value.Location.ShouldBe(command.Location);
    }

    [Fact]
    public async Task PublishingAnEventInvokesTheCustomConsumeFilter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var @event = new WeatherUpdatedEvent(Guid.NewGuid(), "Aarhus", 12);

        using var publishResponse = await fixture.ProducerClient.PostAsJsonAsync(
            "/api/publish-event",
            @event,
            cancellationToken);
        publishResponse.IsSuccessStatusCode.ShouldBeTrue();

        var filterResult = await Polling.WaitAsync(
            PollTimeout,
            ct => TryFindMatchAsync<string>(fixture.ConsumerClient, "/api/received/filter", name => name == nameof(WeatherUpdatedEvent), ct),
            PollInterval,
            cancellationToken);

        filterResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task FlakyConsumerEventuallySucceedsAfterRetries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new FlakyCommand(Guid.NewGuid(), FailUntilAttempt: 3);

        using var sendResponse = await fixture.ProducerClient.PostAsJsonAsync(
            "/api/send-flaky",
            command,
            cancellationToken);
        sendResponse.IsSuccessStatusCode.ShouldBeTrue();

        var successResult = await Polling.WaitAsync(
            PollTimeout,
            ct => TryFindMatchAsync<FlakyCommand>(fixture.ConsumerClient, "/api/received/flaky", c => c.Id == command.Id, ct),
            PollInterval,
            cancellationToken);

        successResult.IsSuccess.ShouldBeTrue();

        var attempts = await fixture.ConsumerClient.GetFromJsonAsync<int>($"/api/attempts/{command.Id}", cancellationToken);
        attempts.ShouldBe(3);
    }

    [Fact]
    public async Task PoisonConsumerDeadLettersAfterRetriesAreExhausted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new PoisonCommand(Guid.NewGuid());

        using var sendResponse = await fixture.ProducerClient.PostAsJsonAsync(
            "/api/send-poison",
            command,
            cancellationToken);
        sendResponse.IsSuccessStatusCode.ShouldBeTrue();

        var connectionString = await fixture.GetRabbitMqConnectionStringAsync(cancellationToken);
        connectionString.ShouldNotBeNull();

        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var deadLetterResult = await Polling.WaitAsync(
            PollTimeout,
            async ct =>
            {
                var message = await channel.BasicGetAsync("poison-commands.dead-letter", autoAck: true, ct);
                return message is not null
                    ? Result.Success(true)
                    : Result.Failure<bool>(Error.NotFound("DeadLetter.Empty", "No dead-lettered message yet."));
            },
            PollInterval,
            cancellationToken);

        deadLetterResult.IsSuccess.ShouldBeTrue();

        var attempts = await fixture.ConsumerClient.GetFromJsonAsync<int>($"/api/attempts/{command.Id}", cancellationToken);
        attempts.ShouldBe(2);
    }

    [Fact]
    public async Task RequestToAFaultingConsumerSurfacesAFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = new FailingRequest("boom");

        using var response = await fixture.ProducerClient.PostAsJsonAsync(
            "/api/request-failing",
            request,
            cancellationToken);

        response.IsSuccessStatusCode.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
    }

    [Fact]
    public async Task RequestWithShortPerRequestTimeoutSurfacesATimeoutFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = new UnansweredRequest("no-listener");

        var stopwatch = Stopwatch.StartNew();
        using var response = await fixture.ProducerClient.PostAsJsonAsync(
            "/api/request-timeout",
            request,
            cancellationToken);
        stopwatch.Stop();

        response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        body.ShouldContain("Request.Timeout");

        // The 2s per-request timeout must apply, well under the 15s global default.
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(12));
    }

    private static async Task<Result<T>> TryFindMatchAsync<T>(
        HttpClient client,
        string endpoint,
        Func<T, bool> predicate,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var items = await client.GetFromJsonAsync<List<T>>(endpoint, cancellationToken);
            var match = items?.FirstOrDefault(predicate);
            return match is not null
                ? Result.Success(match)
                : Result.Failure<T>(Error.NotFound("Polling.NotFound", $"No matching item at {endpoint} yet."));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<T>(Error.Failure("Polling.HttpRequest", ex.Message));
        }
    }
}
