using Catalog.Infrastructure;
using Catalog.Search;
using ServiceDefaults;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application;
using Vulthil.SharedKernel.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddEndpoints(typeof(Program).Assembly);
builder.Services.AddOpenApiServices();
builder.Services.AddProblemDetailsHandling();

builder.Services.AddApplication(application => application
    .AddRequestLoggingBehavior()
    .AddDomainEventLoggingBehavior()
    .AddValidationPipelineBehavior()
    .RegisterHandlerAssemblies(typeof(Program).Assembly)
    .RegisterFluentValidationAssemblies(typeof(Program).Assembly));
builder.Services.AddCatalogSearchModule();

builder.AddCatalogPersistence(ServiceNames.CatalogDatabaseServiceName)
    .AddCatalogMessaging(ServiceNames.RabbitMqServiceName);

var app = builder.Build();

app.UseProblemDetailsHandling();
app.MapDefaultEndpoints();
app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApiEndpoints();

    // Cosmos DB has no migrations; the database and its containers are created from the model instead.
    await app.EnsureCreatedAsync<CatalogDbContext>();
}

await app.RunAsync();
