using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Infrastructure;
using WebApi.Data;
using WebApi.Infrastructure;
using WebApi.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddInfrastructureWithUnitOfWork<WebApiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(ServiceNames.PostgresSqlServerServiceName)));
builder.Services.AddInfrastructure<CosmosDbContext>(options =>
    options.UseCosmos(builder.Configuration.GetConnectionString(ServiceNames.CosmosServiceName)!, "NewDatabase"));

builder.AddInfrastructure(ServiceNames.RabbitMqServiceName);

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapOpenApi();

app.MapPost("/testEvent", async (ILogger<Program> logger, IRequester requester) =>
{
    var someMessage = new TestRequest(Guid.NewGuid(), "some name");
    logger.LogInformation("Sending message: {SomeMessage}", someMessage);

    var result = await requester.RequestAsync<TestRequest, TestEvent>(someMessage);
    return result
        .Tap(t => logger.LogInformation("Received response: {Message}", t))
        .ToIResult();
});

await app.RunAsync();

public partial class Program;
