using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.RabbitMq;

internal sealed class RabbitMqConsumer : AsyncEventingBasicConsumer
{
    private readonly QueueDefinition _queueDefinition;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TypeCache _typeCache;

    public RabbitMqConsumer(IChannel channel, QueueDefinition queueDefinition, IServiceScopeFactory serviceScopeFactory, TypeCache typeCache) : base(channel)
    {
        _queueDefinition = queueDefinition;
        _serviceScopeFactory = serviceScopeFactory;
        _typeCache = typeCache;

        ReceivedAsync += ReceiveMessage;
    }
    private async Task ReceiveMessage(object _, BasicDeliverEventArgs eventArgs)
    {
        await HandleMessageAsync(eventArgs.Body, eventArgs.BasicProperties, eventArgs.CancellationToken);

        await Channel.BasicAckAsync(eventArgs.DeliveryTag, false, eventArgs.CancellationToken);
    }

    private async Task HandleMessageAsync(ReadOnlyMemory<byte> body, IReadOnlyBasicProperties basicProperties, CancellationToken cancellationToken)
    {
        MessageResult? response = null;
        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<RabbitMqConsumer>>();

            var typeString = basicProperties.Type;

            if (string.IsNullOrWhiteSpace(typeString))
            {
                logger.LogInformation("Message without type information arrived on queue '{QueueName}'.", _queueDefinition.Name);
                return;
            }

            if (!_typeCache.TryGetFromString(typeString, out var type))
            {
                logger.LogInformation("Message with type '{Type}' is not configured.", typeString);
                return;
            }

            var handled = false;

            var byteBody = body.ToArray();
            var jsonString = Encoding.UTF8.GetString(byteBody);
            var message = JsonSerializer.Deserialize(jsonString, type.Type);
            if (message is null)
            {
                logger.LogWarning("Message with type '{Type}' could not be deserialized. Raw Message:\n{JsonMessage}", typeString, jsonString);
                return;
            }

            if (!_queueDefinition.Messages.TryGetValue(type, out var consumers))
            {
                logger.LogWarning("Message with type '{Type}' not registered in this service. Raw Message:\n{JsonMessage}", typeString, jsonString);
                return;
            }

            foreach (var consumerType in consumers)
            {
                try
                {
                    var resolvedConsumer = scope.ServiceProvider.GetService(consumerType.Type);

                    var task = resolvedConsumer switch
                    {
                        IConsumer consumer => HandleConsumer(consumer, message, cancellationToken),
                        IRequestConsumer requestConsumer => HandleRequestConsumer(requestConsumer, message, cancellationToken),
                        _ => throw new UnreachableException($"Consumer type '{consumerType.Type}' is not a valid consumer type.")
                    };

                    if (task is Task<MessageResult> messageResultTask)
                    {
                        response = await messageResultTask;
                    }
                    await task;
                    handled = true;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Exception while handling '{Type}' on consumer '{ConsumerType}'.", typeString, consumerType);
                }
            }

            if (!handled)
            {
                logger.LogInformation("Message with type '{Type}' arrived on queue '{QueueName}', but could not be handled.", typeString, _queueDefinition.Name);
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(basicProperties.ReplyTo))
            {
                response ??= MessageResult.Failure("");
                await HandleResponseAsync(response, basicProperties, cancellationToken);
            }
        }
    }

    private static async Task<MessageResult> HandleRequestConsumer(IRequestConsumer requestConsumer, object message, CancellationToken cancellationToken)
    {
        try
        {
            var response = await requestConsumer.ConsumeAsync(message, cancellationToken);

            var responseJsonString = JsonSerializer.Serialize(response, response.GetType());
            return MessageResult.Success(responseJsonString);
        }
        catch (Exception exception)
        {
            return MessageResult.Failure(exception.Message);
        }
    }

    private async Task HandleResponseAsync(MessageResult wrappedResponse, IReadOnlyBasicProperties basicProperties, CancellationToken cancellationToken)
    {
        var jsonString = JsonSerializer.Serialize(wrappedResponse);
        var byteBody = Encoding.UTF8.GetBytes(jsonString);
        var properties = new BasicProperties
        {
            Type = typeof(MessageResult).FullName,
            ContentType = RabbitMqConstants.ContentType,
            CorrelationId = basicProperties.CorrelationId,
        };

        await Channel.BasicPublishAsync(string.Empty, basicProperties.ReplyTo!, true, properties, byteBody, cancellationToken);
    }

    private static Task HandleConsumer(IConsumer consumer, object message, CancellationToken cancellationToken) =>
        consumer.ConsumeAsync(message, cancellationToken);
}
