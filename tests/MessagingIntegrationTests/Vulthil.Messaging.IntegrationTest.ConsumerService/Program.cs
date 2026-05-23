using Vulthil.Messaging;
using Vulthil.Messaging.IntegrationTest.ConsumerService;
using Vulthil.Messaging.IntegrationTest.Contracts;
using Vulthil.Messaging.RabbitMq;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ReceivedMessageTracker>();

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

    messaging.ConfigureQueue("weather-events", queue =>
    {
        queue.AddConsumer<WeatherUpdatedEventConsumer>();
    });

    messaging.ConfigureQueue("weather-commands", queue =>
    {
        queue.AddConsumer<RecordWeatherCommandConsumer>(c => c.Bind<RecordWeatherCommand>("weather.record"));
    });

    messaging.ConfigureQueue("weather-requests", queue =>
    {
        queue.AddRequestConsumer<GetWeatherRequestConsumer>(c => c.Bind<GetWeatherRequest, GetWeatherResponse>("weather.get"));
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

api.MapGet("received/events", (ReceivedMessageTracker tracker) => Results.Ok(tracker.Events))
    .WithName("GetReceivedEvents");

api.MapGet("received/commands", (ReceivedMessageTracker tracker) => Results.Ok(tracker.Commands))
    .WithName("GetReceivedCommands");

api.MapGet("received/requests", (ReceivedMessageTracker tracker) => Results.Ok(tracker.Requests))
    .WithName("GetReceivedRequests");

app.MapDefaultEndpoints();

await app.RunAsync();
