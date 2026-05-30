using Vulthil.Messaging;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Commands;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Events;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Failures;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Infrastructure;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Requests;
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

    messaging.ConfigureMessage<FailingRequest>(message =>
    {
        message.ExchangeType = MessagingExchangeType.Direct;
        message.UseRoutingKey("weather.fail");
    });

    // Cross-cutting consume filter: records the message type of every delivery it wraps.
    messaging.AddOpenConsumeFilter(typeof(AuditConsumeFilter<>));

    messaging.ConfigureQueue("weather-events", queue =>
    {
        queue.AddConsumer<WeatherUpdatedEventConsumer>();
    });

    messaging.ConfigureQueue("weather-commands", queue =>
    {
        queue.Subscribe<RecordWeatherCommand>("weather.record");
        queue.AddConsumer<RecordWeatherCommandConsumer>();
    });

    // Point-to-point target: RecordWeatherCommandConsumer forwards an audit entry here via ctx.SendAsync.
    messaging.ConfigureQueue("weather-audit", queue =>
    {
        queue.AddConsumer<WeatherAuditConsumer>();
    });

    messaging.ConfigureQueue("weather-requests", queue =>
    {
        queue.Subscribe<GetWeatherRequest>("weather.get");
        queue.AddRequestConsumer<GetWeatherRequestConsumer>();
    });

    messaging.ConfigureQueue("failing-requests", queue =>
    {
        queue.Subscribe<FailingRequest>("weather.fail");
        queue.AddRequestConsumer<FailingRequestConsumer>();
    });

    // Polymorphic fan-out: a single StockChangedEvent delivery fires both the interface
    // consumer (IInventoryEvent) and the concrete consumer (StockChangedEvent) on this queue.
    messaging.ConfigureQueue("inventory-events", queue =>
    {
        queue.SubscribeAll<IInventoryEvent>(typeof(IInventoryEvent).Assembly);
        queue.AddConsumer<InventoryEventConsumer>();
        queue.AddConsumer<StockChangedEventConsumer>();
    });

    // Failure scenario: consumer fails the first attempts, then succeeds once retries kick in.
    messaging.ConfigureQueue("flaky-commands", queue =>
    {
        queue.UseRetry(retry => retry.Immediate(5));
        queue.AddConsumer<FlakyCommandConsumer>();
    });

    // Failure scenario: consumer always throws; after retries are exhausted the message is dead-lettered.
    messaging.ConfigureQueue("poison-commands", queue =>
    {
        queue.UseRetry(retry => retry.Immediate(1));
        queue.UseDeadLetterQueue(
            queueName: "poison-commands.dead-letter",
            exchangeName: "poison-commands.dead-letter-exchange");
        queue.AddConsumer<PoisonCommandConsumer>();
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

api.MapGet("received/{key}", (string key, ReceivedMessageTracker tracker) => Results.Ok(tracker.Get(key)))
    .WithName("GetReceivedMessages");

api.MapGet("attempts/{id:guid}", (Guid id, ReceivedMessageTracker tracker) => Results.Ok(tracker.GetAttempts(id)))
    .WithName("GetConsumeAttempts");

app.MapDefaultEndpoints();

await app.RunAsync();
