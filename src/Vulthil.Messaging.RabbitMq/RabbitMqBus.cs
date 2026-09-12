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
    private readonly TimeProvider _timeProvider;
    private readonly List<RabbitMqConsumerWorker> _workers = [];

    public RabbitMqBus(
        IServiceScopeFactory serviceScopeFactory,
        IConnection connection,
        IMessageConfigurationProvider messageConfigurationProvider,
        RabbitMqBusStartupStatus startupStatus,
        ILogger<RabbitMqBus> logger,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _connection = connection;
        _messageConfigurationProvider = messageConfigurationProvider;
        _startupStatus = startupStatus;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _timeProvider = timeProvider;
    }

    /// <remarks>
    /// A failed start disposes any partially-created consumer workers and rethrows without faulting the readiness
    /// signal, so the hosting consumer service can retry a transient failure (such as a broker that is still coming
    /// up) while <see cref="RabbitMqBusStartupStatus.Ready"/> stays pending until a start attempt succeeds. Fresh
    /// per-queue dispatch plans are built for every attempt (rather than kept as instance state) so a retry after a
    /// partial failure starts each queue from an empty registry instead of the handlers (or request-consumer
    /// bookkeeping) left over from the attempt that failed.
    /// </remarks>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var queues = _messageConfigurationProvider.QueueDefinitions;
            MessagingLog.BusStarting(_logger, queues.Count);

            var dispatchPlans = BuildDispatchPlans(queues);
            await SetupTopology(queues, dispatchPlans, cancellationToken).ConfigureAwait(false);
            await StartConsumersAsync(queues, dispatchPlans, cancellationToken).ConfigureAwait(false);

            MessagingLog.BusStarted(_logger);
            _startupStatus.MarkStarted();
        }
        catch (Exception)
        {
            await DisposeWorkersAsync().ConfigureAwait(false);
            throw;
        }
    }

    private Dictionary<string, QueueDispatchPlans> BuildDispatchPlans(IReadOnlyCollection<QueueDefinition> queues)
    {
        var dispatchPlans = new Dictionary<string, QueueDispatchPlans>(StringComparer.OrdinalIgnoreCase);
        foreach (var queue in queues)
        {
            dispatchPlans[queue.Name] = new QueueDispatchPlans(_messageConfigurationProvider, queue);
        }

        return dispatchPlans;
    }

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken = default) =>
        _startupStatus.Ready.WaitAsync(cancellationToken);

    /// <remarks>
    /// A partitioned queue dispatches in FIFO order from a single channel so the worker can assign deliveries to
    /// partition lanes in arrival order; parallelism comes from the lanes (bounded by <c>PrefetchCount</c>) rather
    /// than concurrent dispatch.
    /// </remarks>
    private async Task StartConsumersAsync(IReadOnlyCollection<QueueDefinition> queues, Dictionary<string, QueueDispatchPlans> dispatchPlans, CancellationToken cancellationToken)
    {
        var workerLogger = _loggerFactory.CreateLogger<RabbitMqConsumerWorker>();

        foreach (var queue in queues)
        {
            var plans = dispatchPlans[queue.Name];
            var channelCount = plans.IsPartitioned ? 1 : queue.ChannelCount;
            var dispatchConcurrency = plans.IsPartitioned ? (ushort)1 : queue.ConcurrencyLimit;

            for (int i = 0; i < channelCount; i++)
            {
                var options = new CreateChannelOptions(
                    publisherConfirmationsEnabled: false,
                    publisherConfirmationTrackingEnabled: false,
                    consumerDispatchConcurrency: dispatchConcurrency
                );

                var channel = await _connection.CreateChannelAsync(options, cancellationToken).ConfigureAwait(false);
                await channel.BasicQosAsync(0, queue.PrefetchCount, false, cancellationToken).ConfigureAwait(false);

                var worker = new RabbitMqConsumerWorker(
                    _serviceScopeFactory,
                    queue,
                    channel,
                    plans,
                    _messageConfigurationProvider,
                    workerLogger,
                    _timeProvider,
                    i);

                _workers.Add(worker);
            }
        }
        await Task.WhenAll(_workers.Select(worker => worker.StartAsync(cancellationToken))).ConfigureAwait(false);
    }

    /// <remarks>
    /// The fault exchange is a shared topic exchange: every terminal consume failure publishes a <c>Fault&lt;T&gt;</c>
    /// here by convention with the faulted message's URN as the routing key, so a subscriber binds its queue with
    /// <c>"#"</c> to observe all faults or with a specific URN to filter by faulted message type.
    /// </remarks>
    private async Task SetupTopology(IReadOnlyCollection<QueueDefinition> queues, Dictionary<string, QueueDispatchPlans> dispatchPlans, CancellationToken cancellationToken)
    {
        using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.ExchangeDeclareAsync(
            exchange: _messageConfigurationProvider.FaultExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var queue in queues)
        {
            await SetupQueueTopology(queue, dispatchPlans[queue.Name], channel, cancellationToken).ConfigureAwait(false);
            MessagingLog.QueueDeclared(_logger, queue.Name, queue.Registrations.Count);
        }
    }

    /// <remarks>
    /// A partitioned queue's per-key order only holds within one process; a single active consumer keeps one instance
    /// active (others stand by for failover) so ordering survives across load-balanced consumers. Partitioned queues
    /// opt in automatically; any queue can request it explicitly.
    /// </remarks>
    private async Task SetupQueueTopology(QueueDefinition queue, QueueDispatchPlans plans, IChannel channel, CancellationToken cancellationToken)
    {
        WarnUnresolvableIgnoredExceptions(queue);

        await channel.ExchangeDeclareAsync(
            exchange: queue.Name,
            type: queue.ExchangeType.ToRabbitExchangeType(),
            durable: queue.ExchangeDurable,
            autoDelete: queue.ExchangeAutoDelete,
            arguments: queue.ExchangeArguments,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var args = new Dictionary<string, object?>();
        if (queue.IsQuorum)
        {
            args.Add("x-queue-type", "quorum");
        }

        if (queue.SingleActiveConsumer || plans.IsPartitioned)
        {
            args.Add("x-single-active-consumer", true);
        }

        if (queue.DeadLetter is { Enabled: true })
        {
            var dlx = queue.DeadLetter.ExchangeName ?? $"{queue.Name}.Error";
            var dlq = queue.DeadLetter.QueueName ?? $"{queue.Name}.Error";

            await channel.ExchangeDeclareAsync(dlx, ExchangeType.Fanout, true, cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.QueueDeclareAsync(dlq, true, false, false, arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.QueueBindAsync(dlq, dlx, "#", cancellationToken: cancellationToken).ConfigureAwait(false);

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

            await channel.ExchangeDeclareAsync(retryExchange, ExchangeType.Topic, true, cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.QueueDeclareAsync(retryQueue, true, false, false, retryArgs, cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.QueueBindAsync(retryQueue, retryExchange, "#", cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await channel.QueueDeclareAsync(
            queue: queue.Name,
            durable: queue.IsQuorum || queue.Durable,
            exclusive: !queue.IsQuorum && queue.Exclusive,
            autoDelete: queue.AutoDelete,
            arguments: args,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueBindAsync(
            queue: queue.Name,
            exchange: queue.Name,
            routingKey: string.Empty,
            cancellationToken: cancellationToken).ConfigureAwait(false);

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
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await channel.ExchangeBindAsync(
                destination: queue.Name,
                source: exchangeName,
                routingKey: subscription.RoutingKey ?? string.Empty,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Logs a startup warning for every configured ignored-exception name that does not resolve to a CLR
    /// type: <c>Type.GetType</c> returns <see langword="null"/> for names outside the core library unless
    /// they are assembly-qualified, and a silently unresolved name means the exception the user meant to
    /// exclude is retried anyway.
    /// </summary>
    private void WarnUnresolvableIgnoredExceptions(QueueDefinition queue)
    {
        var policies = new HashSet<RetryPolicyDefinition>(ReferenceEqualityComparer.Instance);
        if (queue.DefaultRetryPolicy is { } defaultPolicy)
        {
            policies.Add(defaultPolicy);
        }

        foreach (var registration in queue.Registrations)
        {
            if (registration.RetryPolicy is { } policy)
            {
                policies.Add(policy);
            }
        }

        var unresolvableNames = policies
            .SelectMany(static policy => policy.IgnoreExceptions)
            .Where(static name => Type.GetType(name, false) is null);
        foreach (var name in unresolvableNames)
        {
            MessagingLog.IgnoredExceptionUnresolvable(_logger, queue.Name, name);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeWorkersAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task DisposeWorkersAsync()
    {
        foreach (var worker in _workers)
        {
            await worker.DisposeAsync().ConfigureAwait(false);
        }

        _workers.Clear();
    }
}
