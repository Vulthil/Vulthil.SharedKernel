using Vulthil.SharedKernel.Messaging;
using Vulthil.SharedKernel.Messaging.Consumers;
using Vulthil.SharedKernel.Messaging.RabbitMq;

namespace WebApi;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMessaging(configurator =>
        {
            configurator.AddQueue("some-queue", queueConfigurator =>
            {
                queueConfigurator
                    .AddConsumer<SomeConsumer>()
                    .AddConsumer<SomeOtherConsumer>();
            });

            configurator.UseRabbitMq(configuration, "RabbitMq");
        });

        return services;
    }
}

public sealed record SomeMessage(Guid Id);
internal sealed class SomeConsumer : IConsumer<SomeMessage>
{
    private readonly ILogger<SomeConsumer> _logger;

    public SomeConsumer(ILogger<SomeConsumer> logger) => _logger = logger;

    public Task ConsumeAsync(SomeMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("{SomeMessage}", message);
        return Task.CompletedTask;
    }
}
internal sealed class SomeOtherConsumer : IConsumer<SomeMessage>
{
    private readonly ILogger<SomeOtherConsumer> _logger;

    public SomeOtherConsumer(ILogger<SomeOtherConsumer> logger) => _logger = logger;

    public Task ConsumeAsync(SomeMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("{SomeMessage}", message);
        return Task.CompletedTask;
    }
}
