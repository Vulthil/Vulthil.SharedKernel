using System.Net.Http.Json;
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
