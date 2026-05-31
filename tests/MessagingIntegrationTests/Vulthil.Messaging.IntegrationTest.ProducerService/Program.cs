using Vulthil.Messaging;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.IntegrationTest.Contracts;
using Vulthil.Messaging.RabbitMq;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.AddMessaging(messaging =>
{
    messaging.ConfigureMessagingOptions(options => options.DefaultTimeout = TimeSpan.FromSeconds(15));

    messaging.ConfigureMessage<WeatherUpdatedEvent>(message =>
    {
        message.ExchangeType = MessagingExchangeType.Fanout;
    });

    messaging.ConfigureMessage<RecordWeatherCommand>(message =>
    {
        message.ExchangeType = MessagingExchangeType.Direct;
        message.UseRoutingKey("weather.record");
    });

    messaging.ConfigureMessage<GetWeatherRequest>(message =>
    {
        message.ExchangeType = MessagingExchangeType.Direct;
        message.UseRoutingKey("weather.get");
    });

    messaging.ConfigureMessage<FailingRequest>(message =>
    {
        message.ExchangeType = MessagingExchangeType.Direct;
        message.UseRoutingKey("weather.fail");
    });

    messaging.UseRabbitMq("rabbitmq");
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var api = app.MapGroup("/api");

api.MapPost("publish-event", async (WeatherUpdatedEvent message, IPublisher publisher, CancellationToken cancellationToken) =>
{
    await publisher.PublishAsync(message, cancellationToken: cancellationToken);
    return Results.Accepted(value: message);
})
.WithName("PublishWeatherUpdatedEvent");

api.MapPost("publish-inventory", async (StockChangedEvent message, IPublisher publisher, CancellationToken cancellationToken) =>
{
    await publisher.PublishAsync(message, cancellationToken: cancellationToken);
    return Results.Accepted(value: message);
})
.WithName("PublishStockChangedEvent");

api.MapPost("publish-ordered", async (OrderedEvent message, IPublisher publisher, CancellationToken cancellationToken) =>
{
    await publisher.PublishAsync(message, cancellationToken: cancellationToken);
    return Results.Accepted(value: message);
})
.WithName("PublishOrderedEvent");

api.MapPost("send-command", async (RecordWeatherCommand message, IPublisher publisher, CancellationToken cancellationToken) =>
{
    await publisher.PublishAsync(message, cancellationToken: cancellationToken);
    return Results.Accepted(value: message);
})
.WithName("SendRecordWeatherCommand");

api.MapPost("send-flaky", async (FlakyCommand message, IPublisher publisher, CancellationToken cancellationToken) =>
{
    await publisher.PublishAsync(message, cancellationToken: cancellationToken);
    return Results.Accepted(value: message);
})
.WithName("SendFlakyCommand");

api.MapPost("send-poison", async (PoisonCommand message, IPublisher publisher, CancellationToken cancellationToken) =>
{
    await publisher.PublishAsync(message, cancellationToken: cancellationToken);
    return Results.Accepted(value: message);
})
.WithName("SendPoisonCommand");

api.MapPost("request", async (GetWeatherRequest message, IRequester requester, CancellationToken cancellationToken) =>
{
    var result = await requester.RequestAsync<GetWeatherRequest, GetWeatherResponse>(message, cancellationToken: cancellationToken);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Problem(detail: result.Error.Description, title: result.Error.Code, statusCode: StatusCodes.Status504GatewayTimeout);
})
.WithName("RequestWeather");

api.MapPost("request-failing", async (FailingRequest message, IRequester requester, CancellationToken cancellationToken) =>
{
    var result = await requester.RequestAsync<FailingRequest, FailingResponse>(message, cancellationToken: cancellationToken);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Problem(detail: result.Error.Description, title: result.Error.Code, statusCode: StatusCodes.Status504GatewayTimeout);
})
.WithName("RequestFailing");

api.MapPost("request-timeout", async (UnansweredRequest message, IRequester requester, CancellationToken cancellationToken) =>
{
    var result = await requester.RequestAsync<UnansweredRequest, UnansweredResponse>(
        message,
        context =>
        {
            context.SetTimeout(TimeSpan.FromSeconds(2));
            return Task.CompletedTask;
        },
        cancellationToken);

    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Problem(detail: result.Error.Description, title: result.Error.Code, statusCode: StatusCodes.Status504GatewayTimeout);
})
.WithName("RequestTimeout");

app.MapDefaultEndpoints();

await app.RunAsync();
