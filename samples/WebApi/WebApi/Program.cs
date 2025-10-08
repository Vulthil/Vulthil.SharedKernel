using WebApi.Application;
using WebApi.Infrastructure;
using WebApi.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddApplicationLayer();
builder.AddDatabaseInfrastructure(ServiceNames.PostgresSqlServerServiceName)
    .AddRabbitMqMessagingInfrastructure(ServiceNames.RabbitMqServiceName);

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapOpenApi();

await app.RunAsync();

#pragma warning disable S1118 // Utility classes should not have public constructors
public partial class Program;
#pragma warning restore S1118 // Utility classes should not have public constructors
