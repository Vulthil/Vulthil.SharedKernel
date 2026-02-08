using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Consumers;
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
        using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        foreach (var queue in _queueDefinitions)
        {
            await SetupTopology(queue, channel, cancellationToken);

            _typeCache.RegisterQueue(queue);

            var options = new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false,
                consumerDispatchConcurrency: queue.ConsumerCount
            );

            var workerChannel = await _connection.CreateChannelAsync(options, cancellationToken);
            var worker = new RabbitMqConsumerWorker(_serviceScopeFactory, queue, workerChannel, _typeCache, _messagingJsonOptions.JsonSerializerOptions);

            await worker.StartAsync(cancellationToken);

            _workers.Add(worker);
        }

        await _responseListener.InitializeAsync(_connection);
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
            var bindingPattern = GetRoutingKey(registration);

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

    private static string GetRoutingKey(Registration registration) =>
        registration.ConsumerType.Type.GetCustomAttribute<RoutingKeyAttribute>()?.Pattern ?? registration.RoutingKey;

    public async ValueTask DisposeAsync()
    {
        foreach (var worker in _workers)
        {
            await worker.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

}
