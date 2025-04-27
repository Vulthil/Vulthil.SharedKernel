using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure;
using Vulthil.SharedKernel.Messaging.Publishers;
using WebApi;
using WebApi.Data;
using WebApi.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();

builder.AddRabbitMq(ServiceNames.RabbitMqServiceName);

builder.Services.AddInfrastructureWithUnitOfWork<WebApiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(ServiceNames.PostgresSqlServerServiceName)));

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapOpenApi();

app.MapPost("/someMessage", async (ILogger<Program> logger, IPublisher publisher) =>
{
    var someMessage = new SomeMessage(Guid.NewGuid());
    logger.LogInformation("Sending message: {SomeMessage}", someMessage);
    await publisher.PublishAsync(someMessage);
    return Results.NoContent();
});

await app.RunAsync();

public partial class Program;
