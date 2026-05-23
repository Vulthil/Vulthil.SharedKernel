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

api.MapPost("send-command", async (RecordWeatherCommand message, IPublisher publisher, CancellationToken cancellationToken) =>
{
    await publisher.PublishAsync(message, cancellationToken: cancellationToken);
    return Results.Accepted(value: message);
})
.WithName("SendRecordWeatherCommand");

api.MapPost("request", async (GetWeatherRequest message, IRequester requester, CancellationToken cancellationToken) =>
{
    var result = await requester.RequestAsync<GetWeatherRequest, GetWeatherResponse>(message, cancellationToken: cancellationToken);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Problem(detail: result.Error.Description, title: result.Error.Code, statusCode: StatusCodes.Status504GatewayTimeout);
})
.WithName("RequestWeather");

app.MapDefaultEndpoints();

await app.RunAsync();
