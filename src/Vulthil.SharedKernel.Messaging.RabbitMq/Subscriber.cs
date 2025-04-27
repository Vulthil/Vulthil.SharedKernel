using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.SharedKernel.Messaging.Consumers;

namespace Vulthil.SharedKernel.Messaging.RabbitMq;

public sealed class Subscriber : BackgroundService
{
    private readonly ILogger<Subscriber> _logger;
    private readonly RabbitMqConnectionFactory _rabbitMqConnectionFactory;
    private readonly TypeCache _typeCache;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IEnumerable<QueueDefinition> _queueDefinitions;

    public Subscriber(
        ILogger<Subscriber> logger,
        RabbitMqConnectionFactory rabbitMqConnectionFactory,
        TypeCache typeCache,
        IServiceScopeFactory serviceScopeFactory,
        IEnumerable<QueueDefinition> queueDefinitions)
    {
        _logger = logger;
        _rabbitMqConnectionFactory = rabbitMqConnectionFactory;
        _typeCache = typeCache;
        _serviceScopeFactory = serviceScopeFactory;
        _queueDefinitions = queueDefinitions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await _rabbitMqConnectionFactory.CreateConnectionAsync(stoppingToken);

        foreach (var queueDefinition in _queueDefinitions)
        {
            var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await DeclareQueueAndExchanges(channel, queueDefinition);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (ch, ea) => ReceiveMessage(ch, ea, channel, queueDefinition);
            await channel.BasicConsumeAsync(queueDefinition.Name, false, consumer, stoppingToken);
        }
    }

    private static async Task DeclareQueueAndExchanges(IChannel channel, QueueDefinition queueDefinition)
    {
        await channel.ExchangeDeleteAsync(queueDefinition.Name);
        await channel.ExchangeDeclareAsync(queueDefinition.Name, ExchangeType.Fanout, true, false);
        await channel.QueueDeclareAsync(queueDefinition.Name, true, false, false);
        await channel.QueueBindAsync(queueDefinition.Name, queueDefinition.Name, "");

        var messageTypes = queueDefinition.Consumers.Values.SelectMany(t => t).Distinct();
        foreach (var messageType in messageTypes)
        {
            await channel.ExchangeDeclareAsync(messageType.FullName!, ExchangeType.Fanout, true, false);
            await channel.ExchangeBindAsync(queueDefinition.Name, messageType.FullName!, "");
        }
    }

    private async Task ReceiveMessage(object _, BasicDeliverEventArgs eventArgs, IChannel channel, QueueDefinition queueDefinition)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        var shouldAck = await HandleMessageAsync(eventArgs, queueDefinition, scope);
        if (shouldAck)
        {
            await channel.BasicAckAsync(eventArgs.DeliveryTag, false, eventArgs.CancellationToken);
        }
    }

    private async Task<bool> HandleMessageAsync(BasicDeliverEventArgs eventArgs, QueueDefinition queueDefinition, AsyncServiceScope scope)
    {
        var headers = eventArgs.BasicProperties.Headers;

        var typeString = eventArgs.BasicProperties.Type;

        if (string.IsNullOrWhiteSpace(typeString))
        {
            _logger.LogInformation("Message without type information arrived on queue '{QueueName}'.", queueDefinition.Name);
            return true;
        }

        if (!_typeCache.TryGetFromString(typeString, out var type))
        {
            _logger.LogInformation("Message with type '{Type}' is not configured.", typeString);
            return true;
        }

        bool handled = false;

        var byteBody = eventArgs.Body.ToArray();
        var jsonString = Encoding.UTF8.GetString(byteBody);
        var message = JsonSerializer.Deserialize(jsonString, type);
        var genericMethod = ConnectConsumerMethod.MakeGenericMethod(typeof(IConsumer<>).MakeGenericType(type), type);

        foreach (var (consumerType, messageTypes) in queueDefinition.Consumers)
        {
            try
            {
                if (!messageTypes.Contains(type))
                {
                    continue;
                }

                var consumer = scope.ServiceProvider.GetService(consumerType);
                var consumeMethod = (Task)genericMethod.Invoke(null, [consumer, message, eventArgs.CancellationToken])!;
                await consumeMethod;
                handled = true;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Exception while handling '{Type}' on consumer '{ConsumerType}'.", typeString, consumerType);
            }
        }

        if (!handled)
        {
            _logger.LogInformation("Message with type '{Type}' arrived on queue '{QueueName}', but could not be handled.", typeString, queueDefinition.Name);
        }

        return true;
    }

    private static readonly MethodInfo ConnectConsumerMethod = typeof(Subscriber).GetMethod(nameof(ConnectConsumer), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static async Task ConnectConsumer<TConsumer, TMessage>(TConsumer consumer, TMessage message, CancellationToken cancellationToken)
        where TConsumer : IConsumer<TMessage>
        where TMessage : class
    {
        await consumer.ConsumeAsync(message, cancellationToken);
    }

}
