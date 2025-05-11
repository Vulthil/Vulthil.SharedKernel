using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Vulthil.SharedKernel.Messaging.RabbitMq;

public sealed class RabbitMqHostedService : BackgroundService
{
    private readonly ILogger<RabbitMqHostedService> _logger;
    private readonly IConnection _rabbitMqConnection;
    private readonly TypeCache _typeCache;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IEnumerable<QueueDefinition> _queueDefinitions;

    public RabbitMqHostedService(
        ILogger<RabbitMqHostedService> logger,
        IConnection rabbitMqConnection,
        TypeCache typeCache,
        IServiceScopeFactory serviceScopeFactory,
        IEnumerable<QueueDefinition> queueDefinitions)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _typeCache = typeCache;
        _serviceScopeFactory = serviceScopeFactory;
        _queueDefinitions = queueDefinitions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await DeclareQueueAndExchanges(stoppingToken);

        await StartConsumers(stoppingToken);
    }

    private async Task StartConsumers(CancellationToken cancellationToken)
    {
        foreach (var queueDefinition in _queueDefinitions)
        {
            var createChannelOptions = new CreateChannelOptions(false, false, consumerDispatchConcurrency: queueDefinition.ConsumerCount);
            var channel = await _rabbitMqConnection.CreateChannelAsync(createChannelOptions, cancellationToken: cancellationToken);
            await channel.BasicQosAsync(0, queueDefinition.PrefetchCount, false, cancellationToken);

            var consumer = new RabbitMqConsumer(channel, queueDefinition, _serviceScopeFactory, _typeCache);

            await channel.BasicConsumeAsync(queueDefinition.Name, false, consumer, cancellationToken);
        }
    }

    private async Task DeclareQueueAndExchanges(CancellationToken cancellationToken)
    {
        await using var channel = await _rabbitMqConnection.CreateChannelAsync(cancellationToken: cancellationToken);
        foreach (var queueDefinition in _queueDefinitions)
        {
            await channel.ExchangeDeleteAsync(queueDefinition.Name, cancellationToken: cancellationToken);
            await channel.ExchangeDeclareAsync(queueDefinition.Name, ExchangeType.Fanout, true, false, cancellationToken: cancellationToken);
            await channel.QueueDeclareAsync(queueDefinition.Name, true, false, false, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(queueDefinition.Name, queueDefinition.Name, "", cancellationToken: cancellationToken);

            foreach (var messageType in queueDefinition.Messages.Keys)
            {
                await channel.ExchangeDeclareAsync(messageType.Name, ExchangeType.Fanout, true, false, cancellationToken: cancellationToken);
                await channel.ExchangeBindAsync(queueDefinition.Name, messageType.Name!, "", cancellationToken: cancellationToken);
            }
        }
    }
}
