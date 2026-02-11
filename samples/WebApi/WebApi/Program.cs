using System.Reflection;
using ServiceDefaults;
using Vulthil.SharedKernel.Api;
using WebApi.Application;
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

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApiEndpoints();
    await app.MigrateAsync();
}

await app.RunAsync();

#pragma warning disable S1118 // Utility classes should not have public constructors
public partial class Program;
#pragma warning restore S1118 // Utility classes should not have public constructors
