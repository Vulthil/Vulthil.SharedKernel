using ServiceDefaults;
using Shipments.Application;
using Shipments.Application.Pipeline;
using Shipments.Infrastructure;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application;
using Vulthil.SharedKernel.Infrastructure.Relational;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddEndpoints(typeof(Program).Assembly);
builder.Services.AddOpenApiServices();
builder.Services.AddProblemDetailsHandling();

builder.Services.AddApplication(application => application
    .AddRequestLoggingPipelineBehavior()
    .AddDomainEventLoggingPipelineBehavior()
    .AddValidationPipelineBehavior()
    .AddOpenDomainEventPipelineHandler(typeof(DomainEventTimingHandler<>))
    .RegisterHandlerAssemblies(typeof(Program).Assembly)
    .RegisterFluentValidationAssemblies(typeof(Program).Assembly));

builder.AddShipmentsModule()
    .AddShipmentsPersistence(ServiceNames.ShipmentsDatabaseServiceName)
    .AddShipmentsMessaging(ServiceNames.RabbitMqServiceName);

var app = builder.Build();

app.UseProblemDetailsHandling();
app.MapDefaultEndpoints();
app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApiEndpoints();
    await app.MigrateAsync<ShipmentsDbContext>();
}

await app.RunAsync();
