using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulthil.Messaging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.RabbitMq;

namespace WebApi.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder, string rabbitMqConnectionStringKey)
    {
        builder.Services.AddMessaging(builder.Configuration, x =>
        {
            x.AddQueue("test", queue =>
            {
                queue.AddConsumer<TestConsumer>();
                queue.AddRequestConsumer<TestRequestConsumer>();
            });

            x.AddRequest<TestRequest>("test");

            x.UseRabbitMq();
        });

        builder.AddRabbitMqClient(rabbitMqConnectionStringKey);

        return builder;
    }
}

public sealed record TestEvent(Guid Id, string Name);
public sealed record TestRequest(Guid Id, string Name);
internal sealed class TestConsumer(ILogger<TestConsumer> logger) : IConsumer<TestRequest>
{
    private readonly ILogger<TestConsumer> _logger = logger;

    public Task ConsumeAsync(TestRequest message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received message: {Message}", message);
        return Task.CompletedTask;
    }
}
internal class TestRequestConsumer(ILogger<TestRequestConsumer> logger) : IRequestConsumer<TestRequest, TestEvent>
{
    private readonly ILogger<TestRequestConsumer> _logger = logger;
    public Task<TestEvent> ConsumeAsync(TestRequest message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received request: {Message}", message);
        return Task.FromResult(new TestEvent(Guid.NewGuid(), message.Name));
    }
}
