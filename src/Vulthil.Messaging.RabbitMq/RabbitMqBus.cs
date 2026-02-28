using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.Messaging.RabbitMq.Requests;

namespace Vulthil.Messaging.RabbitMq;


internal sealed class RabbitMqBus : ITransport, IAsyncDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConnection _connection;
    private readonly ResponseListener _responseListener;
    private readonly IEnumerable<QueueDefinition> _queueDefinitions;
    private readonly MessagingOptions _messagingJsonOptions;
    private readonly MessageTypeCache _typeCache = new();
    private readonly List<RabbitMqConsumerWorker> _workers = [];

    public RabbitMqBus(
        IServiceScopeFactory serviceScopeFactory,
        IConnection connection,
        ResponseListener responseListener,
        IEnumerable<QueueDefinition> queueDefinitions,
        IOptions<MessagingOptions> messagingJsonOptions)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _connection = connection;
        _responseListener = responseListener;
        _queueDefinitions = queueDefinitions;
        _messagingJsonOptions = messagingJsonOptions.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await SetupTopology(cancellationToken);

        await StartConsumersAsync(cancellationToken);

        await _responseListener.InitializeAsync(_connection);
    }

    private async Task StartConsumersAsync(CancellationToken cancellationToken)
    {

        foreach (var queue in _queueDefinitions)
        {
            _typeCache.RegisterQueue(queue, _messagingJsonOptions);

            for (int i = 0; i < queue.ChannelCount; i++)
            {
                var options = new CreateChannelOptions(
                    publisherConfirmationsEnabled: false,
                    publisherConfirmationTrackingEnabled: false,
                    consumerDispatchConcurrency: queue.ConcurrencyLimit
                );

                var channel = await _connection.CreateChannelAsync(options, cancellationToken);
                await channel.BasicQosAsync(0, queue.PrefetchCount, false, cancellationToken);

                var worker = new RabbitMqConsumerWorker(_serviceScopeFactory, queue, channel, _typeCache, _messagingJsonOptions.JsonSerializerOptions);

                _workers.Add(worker);
            }
        }
        await Task.WhenAll(_workers.Select(worker => worker.StartAsync(cancellationToken)));
    }

    private async Task SetupTopology(CancellationToken cancellationToken)
    {
        using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        foreach (var queue in _queueDefinitions)
        {
            await SetupTopology(queue, channel, cancellationToken);
        }
    }

    private static async Task SetupTopology(QueueDefinition queue, IChannel channel, CancellationToken cancellationToken)
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

            await channel.ExchangeDeclareAsync(retryExchange, ExchangeType.Direct, true, cancellationToken: cancellationToken);
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

        var registrations = queue.Registrations;

        foreach (var registration in registrations)
        {
            var exchangeName = registration.MessageType.Name;
            var bindingPattern = RabbitMqConstants.GetRoutingKey(registration);

            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true,
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
