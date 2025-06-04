using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application;
using WebApi.Infrastructure;
using WebApi.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddApplication(appOptions =>
{
    appOptions.AddRequestLoggingBehavior()
        .AddDomainEventLoggingBehavior()
        .AddValidationPipelineBehavior()
        .AddTransactionalPipelineBehavior()
        .RegisterMediatRAssemblies(typeof(Program).Assembly)
        .RegisterFluentValidationAssemblies(typeof(Program).Assembly);
});

builder.AddDatabaseInfrastructure(ServiceNames.PostgresSqlServerServiceName)
    .AddRabbitMqMessagingInfrastructure(ServiceNames.RabbitMqServiceName);

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
