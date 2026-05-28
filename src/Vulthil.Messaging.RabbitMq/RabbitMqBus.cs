using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.Messaging.RabbitMq.HealthChecks;
using Vulthil.Messaging.RabbitMq.Logging;

namespace Vulthil.Messaging.RabbitMq;

internal sealed class RabbitMqBus : ITransport, IAsyncDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConnection _connection;
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;
    private readonly RabbitMqBusStartupStatus _startupStatus;
    private readonly ILogger<RabbitMqBus> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MessageTypeCache _typeCache;
    private readonly List<RabbitMqConsumerWorker> _workers = [];

    public RabbitMqBus(
        IServiceScopeFactory serviceScopeFactory,
        IConnection connection,
        IMessageConfigurationProvider messageConfigurationProvider,
        RabbitMqBusStartupStatus startupStatus,
        ILogger<RabbitMqBus> logger,
        ILoggerFactory loggerFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _connection = connection;
        _messageConfigurationProvider = messageConfigurationProvider;
        _startupStatus = startupStatus;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _typeCache = new MessageTypeCache(messageConfigurationProvider);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var queues = _messageConfigurationProvider.QueueDefinitions;
            MessagingLog.BusStarting(_logger, queues.Count);

            await SetupTopology(queues, cancellationToken);
            await StartConsumersAsync(queues, cancellationToken);

            MessagingLog.BusStarted(_logger);
            _startupStatus.MarkStarted();
        }
        catch (Exception ex)
        {
            _startupStatus.MarkFailed(ex);
            throw;
        }
    }

    private async Task StartConsumersAsync(IReadOnlyCollection<QueueDefinition> queues, CancellationToken cancellationToken)
    {
        var workerLogger = _loggerFactory.CreateLogger<RabbitMqConsumerWorker>();

        foreach (var queue in queues)
        {
            _typeCache.RegisterQueue(queue);

            for (int i = 0; i < queue.ChannelCount; i++)
            {
                var options = new CreateChannelOptions(
                    publisherConfirmationsEnabled: false,
                    publisherConfirmationTrackingEnabled: false,
                    consumerDispatchConcurrency: queue.ConcurrencyLimit
                );

                var channel = await _connection.CreateChannelAsync(options, cancellationToken);
                await channel.BasicQosAsync(0, queue.PrefetchCount, false, cancellationToken);

                var worker = new RabbitMqConsumerWorker(
                    _serviceScopeFactory,
                    queue,
                    channel,
                    _typeCache,
                    _messageConfigurationProvider,
                    workerLogger,
                    i);

                _workers.Add(worker);
            }
        }
        await Task.WhenAll(_workers.Select(worker => worker.StartAsync(cancellationToken)));
    }

    private async Task SetupTopology(IReadOnlyCollection<QueueDefinition> queues, CancellationToken cancellationToken)
    {
        using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        foreach (var queue in queues)
        {
            await SetupQueueTopology(queue, channel, cancellationToken);
            MessagingLog.QueueDeclared(_logger, queue.Name, queue.Registrations.Count);
        }
    }

    private async Task SetupQueueTopology(QueueDefinition queue, IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchange: queue.Name,
            type: queue.ExchangeType.ToRabbitExchangeType(),
            durable: queue.ExchangeDurable,
            autoDelete: queue.ExchangeAutoDelete,
            arguments: queue.ExchangeArguments,
            cancellationToken: cancellationToken);

        var args = new Dictionary<string, object?>();
        if (queue.IsQuorum)
        {
            args.Add("x-queue-type", "quorum");
        }

        if (queue.DeadLetter is { Enabled: true })
        {
            var dlx = queue.DeadLetter.ExchangeName ?? $"{queue.Name}.Error";
            var dlq = queue.DeadLetter.QueueName ?? $"{queue.Name}.Error";

            await channel.ExchangeDeclareAsync(dlx, ExchangeType.Fanout, true, cancellationToken: cancellationToken);
            await channel.QueueDeclareAsync(dlq, true, false, false, arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(dlq, dlx, "#", cancellationToken: cancellationToken);

            args.Add("x-dead-letter-exchange", dlx);
        }

        if (queue.RetryEnabled)
        {
            var retryExchange = $"{queue.Name}.Retry";
            var retryQueue = $"{queue.Name}.Retry";

            var retryArgs = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = queue.Name,
                ["x-queue-type"] = "quorum",
            };

            await channel.ExchangeDeclareAsync(retryExchange, ExchangeType.Topic, true, cancellationToken: cancellationToken);
            await channel.QueueDeclareAsync(retryQueue, true, false, false, retryArgs, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(retryQueue, retryExchange, "#", cancellationToken: cancellationToken);
        }

        await channel.QueueDeclareAsync(
            queue: queue.Name,
            durable: queue.IsQuorum || queue.Durable,
            exclusive: !queue.IsQuorum && queue.Exclusive,
            autoDelete: queue.AutoDelete,
            arguments: args,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: queue.Name,
            exchange: queue.Name,
            routingKey: "#",
            cancellationToken: cancellationToken);

        foreach (var registration in queue.Registrations)
        {
            var messageConfig = _messageConfigurationProvider.GetMessageConfiguration(registration.MessageType.Type);
            var exchangeName = messageConfig.Exchange;
            var bindingPattern = RabbitMqConstants.GetRoutingKey(registration);

            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: messageConfig.ExchangeType.ToRabbitExchangeType(),
                durable: messageConfig.Durable,
                autoDelete: messageConfig.AutoDelete,
                arguments: messageConfig.Arguments,
                cancellationToken: cancellationToken);

            await channel.ExchangeBindAsync(
                destination: queue.Name,
                source: exchangeName,
                routingKey: bindingPattern,
                cancellationToken: cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var worker in _workers)
        {
            await worker.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
