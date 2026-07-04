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
    }

    /// <remarks>
    /// A failed start disposes any partially-created consumer workers and rethrows without faulting the readiness
    /// signal, so the hosting consumer service can retry a transient failure (such as a broker that is still coming
    /// up) while <see cref="RabbitMqBusStartupStatus.Ready"/> stays pending until a start attempt succeeds. Fresh
    /// per-queue type caches are built for every attempt (rather than kept as instance state) so a retry after a
    /// partial failure re-registers each queue against an empty registry instead of appending to handlers (or
    /// request-consumer bookkeeping) left over from the attempt that failed.
    /// </remarks>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var queues = _messageConfigurationProvider.QueueDefinitions;
            MessagingLog.BusStarting(_logger, queues.Count);

            var typeCaches = BuildTypeCaches(queues);
            await SetupTopology(queues, typeCaches, cancellationToken);
            await StartConsumersAsync(queues, typeCaches, cancellationToken);

            MessagingLog.BusStarted(_logger);
            _startupStatus.MarkStarted();
        }
        catch (Exception)
        {
            await DisposeWorkersAsync();
            throw;
        }
    }

    /// <summary>
    /// Builds one <see cref="MessageTypeCache"/> per queue, each registering only that queue. Several queues may
    /// consume the same message type, and the broker delivers a distinct copy to each of them; scoping the plan
    /// cache to its queue keeps a delivery from also dispatching the consumers every other queue registered for
    /// that type.
    /// </summary>
    internal Dictionary<string, MessageTypeCache> BuildTypeCaches(IReadOnlyCollection<QueueDefinition> queues)
    {
        var typeCaches = new Dictionary<string, MessageTypeCache>(StringComparer.OrdinalIgnoreCase);
        foreach (var queue in queues)
        {
            var typeCache = new MessageTypeCache(_messageConfigurationProvider);
            typeCache.RegisterQueue(queue);
            typeCaches[queue.Name] = typeCache;
        }

        return typeCaches;
    }

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken = default) =>
        _startupStatus.Ready.WaitAsync(cancellationToken);

    /// <remarks>
    /// A partitioned queue dispatches in FIFO order from a single channel so the worker can assign deliveries to
    /// partition lanes in arrival order; parallelism comes from the lanes (bounded by <c>PrefetchCount</c>) rather
    /// than concurrent dispatch.
    /// </remarks>
    private async Task StartConsumersAsync(IReadOnlyCollection<QueueDefinition> queues, Dictionary<string, MessageTypeCache> typeCaches, CancellationToken cancellationToken)
    {
        var workerLogger = _loggerFactory.CreateLogger<RabbitMqConsumerWorker>();

        foreach (var queue in queues)
        {
            var typeCache = typeCaches[queue.Name];
            var partitioned = typeCache.IsQueuePartitioned(queue);
            var channelCount = partitioned ? 1 : queue.ChannelCount;
            var dispatchConcurrency = partitioned ? (ushort)1 : queue.ConcurrencyLimit;

            for (int i = 0; i < channelCount; i++)
            {
                var options = new CreateChannelOptions(
                    publisherConfirmationsEnabled: false,
                    publisherConfirmationTrackingEnabled: false,
                    consumerDispatchConcurrency: dispatchConcurrency
                );

                var channel = await _connection.CreateChannelAsync(options, cancellationToken);
                await channel.BasicQosAsync(0, queue.PrefetchCount, false, cancellationToken);

                var worker = new RabbitMqConsumerWorker(
                    _serviceScopeFactory,
                    queue,
                    channel,
                    typeCache,
                    _messageConfigurationProvider,
                    workerLogger,
                    i,
                    partitioned);

                _workers.Add(worker);
            }
        }
        await Task.WhenAll(_workers.Select(worker => worker.StartAsync(cancellationToken)));
    }

    /// <remarks>
    /// The fault exchange is a shared topic exchange: every terminal consume failure publishes a <c>Fault&lt;T&gt;</c>
    /// here by convention with the faulted message's URN as the routing key, so a subscriber binds its queue with
    /// <c>"#"</c> to observe all faults or with a specific URN to filter by faulted message type.
    /// </remarks>
    private async Task SetupTopology(IReadOnlyCollection<QueueDefinition> queues, Dictionary<string, MessageTypeCache> typeCaches, CancellationToken cancellationToken)
    {
        using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _messageConfigurationProvider.FaultExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        foreach (var queue in queues)
        {
            await SetupQueueTopology(queue, typeCaches[queue.Name], channel, cancellationToken);
            MessagingLog.QueueDeclared(_logger, queue.Name, queue.Registrations.Count);
        }
    }

    /// <remarks>
    /// A partitioned queue's per-key order only holds within one process; a single active consumer keeps one instance
    /// active (others stand by for failover) so ordering survives across load-balanced consumers. Partitioned queues
    /// opt in automatically; any queue can request it explicitly.
    /// </remarks>
    private async Task SetupQueueTopology(QueueDefinition queue, MessageTypeCache typeCache, IChannel channel, CancellationToken cancellationToken)
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

        if (queue.SingleActiveConsumer || typeCache.IsQueuePartitioned(queue))
        {
            args.Add("x-single-active-consumer", true);
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
            routingKey: string.Empty,
            cancellationToken: cancellationToken);

        foreach (var subscription in queue.Subscriptions)
        {
            var messageConfig = _messageConfigurationProvider.GetMessageConfiguration(subscription.MessageType.Type);
            var exchangeName = messageConfig.Exchange;

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
                routingKey: subscription.RoutingKey ?? string.Empty,
                cancellationToken: cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeWorkersAsync();
        GC.SuppressFinalize(this);
    }

    private async Task DisposeWorkersAsync()
    {
        foreach (var worker in _workers)
        {
            await worker.DisposeAsync();
        }

        _workers.Clear();
    }
}
