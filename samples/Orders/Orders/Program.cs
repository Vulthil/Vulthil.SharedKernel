using Orders.Application;
using Orders.Endpoints;
using Orders.ExternalServices;
using Orders.Infrastructure;
using ServiceDefaults;
using Vulthil.SharedKernel.Api;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddEndpoints(typeof(Program).Assembly);
builder.Services.AddOpenApiServices();
builder.Services.AddProblemDetailsHandling();

builder.Services.AddOrdersApplication();
builder.AddOrdersPersistence(ServiceNames.OrdersDatabaseServiceName)
    .AddOrdersMessaging(ServiceNames.RabbitMqServiceName);

builder.Services.AddHttpClient<IShippingRatesClient, ShippingRatesClient>(
    client => client.BaseAddress = new Uri(builder.Configuration["ExternalServices:ShippingRates"] ?? "https://rates.example.com"));
builder.Services.AddHttpClient(GetStock.InventoryClientName,
    client => client.BaseAddress = new Uri(builder.Configuration["ExternalServices:Inventory"] ?? "https://inventory.example.com"));

var app = builder.Build();

app.UseProblemDetailsHandling();
app.MapDefaultEndpoints();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapEndpoints(app.MapGroup("api").WithTags("Orders"));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApiEndpoints();
    await app.MigrateAsync();
}

await app.RunAsync();
