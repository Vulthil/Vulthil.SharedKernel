using System.Reflection;
using ServiceDefaults;
using Vulthil.SharedKernel.Api;
using WebApi.Application;
using WebApi.ExternalServices;
using WebApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());
builder.Services.AddOpenApiServices();

builder.Services.AddApplicationLayer();
builder.AddDatabaseInfrastructure(ServiceNames.PostgresSqlServerServiceName)
    .AddRabbitMqMessagingInfrastructure(ServiceNames.RabbitMqServiceName);

builder.Services.AddHttpClient<IExternalWeatherClient, ExternalWeatherClient>(
    client => client.BaseAddress = new Uri("https://weather.example.com"));

builder.Services.AddHttpClient("inventory", client => client.BaseAddress = new Uri("https://inventory.example.com"));

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
var apiGroup = app.MapGroup("api").WithTags("API Endpoints");
app.MapEndpoints(apiGroup);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApiEndpoints();
    await app.MigrateAsync();
}

await app.RunAsync();
