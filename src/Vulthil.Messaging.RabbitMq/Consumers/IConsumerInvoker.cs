using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Requests;

namespace Vulthil.Messaging.RabbitMq.Consumers;


internal interface IConsumerInvoker
{
    string RoutingKey { get; }
    RetryPolicyDefinition? RetryPolicy { get; }
    Task InvokeAsync(IServiceProvider sp, object message, BasicDeliverEventArgs ea, CancellationToken ct);
}
internal sealed class ConsumerInvoker<TConsumer, TMessage>(string routingKey, RetryPolicyDefinition? retryPolicy) : IConsumerInvoker
    where TConsumer : class, IConsumer<TMessage>
    where TMessage : notnull
{
    public string RoutingKey => routingKey;

    public RetryPolicyDefinition? RetryPolicy => retryPolicy;

    public async Task InvokeAsync(
        IServiceProvider sp,
        object message,
        BasicDeliverEventArgs ea,
        CancellationToken ct)
    {
        var consumer = sp.GetRequiredService<TConsumer>();
        var context = MessageContext.CreateContext((TMessage)message, ea);

        await consumer.ConsumeAsync(context, ct);
    }
}

internal interface IRpcInvoker
{
    string RoutingKey { get; }
    RetryPolicyDefinition? RetryPolicy { get; }
    Task InvokeAsync(IServiceProvider sp, object message, BasicDeliverEventArgs ea, IChannel channel, CancellationToken ct);
}

internal sealed class RpcInvoker<TConsumer, TRequest, TResponse>(MessagingOptions messagingOptions, string routingKey, RetryPolicyDefinition? retryPolicy) : IRpcInvoker
    where TConsumer : class, IRequestConsumer<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : notnull
{
    private readonly MessagingOptions _messagingOptions = messagingOptions;
    private JsonSerializerOptions _jsonOptions => _messagingOptions.JsonSerializerOptions;
    public string RoutingKey => routingKey;
    public RetryPolicyDefinition? RetryPolicy => retryPolicy;

    public async Task InvokeAsync(IServiceProvider sp, object message, BasicDeliverEventArgs ea, IChannel channel, CancellationToken ct)
    {
        var consumer = sp.GetRequiredService<TConsumer>();

        var context = MessageContext.CreateContext((TRequest)message, ea);

        MessageResult messageResult;
        try
        {
            // Execute consumer and get response
            TResponse response = await consumer.ConsumeAsync(context, ct);

            var responseByteArray = JsonSerializer.SerializeToUtf8Bytes(response, _jsonOptions);

            messageResult = MessageResult.Success(responseByteArray);
        }
        catch (Exception exception)
        {
            messageResult = MessageResult.Failure(exception.Message);
        }

        await SendResponseAsync(ea, messageResult, channel);
    }

    private async Task SendResponseAsync(BasicDeliverEventArgs ea, MessageResult response, IChannel channel)
    {
        // SEND RESPONSE
        if (!string.IsNullOrEmpty(ea.BasicProperties.ReplyTo))
        {
            var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response, _jsonOptions);
            var replyProps = new BasicProperties
            {
                CorrelationId = ea.BasicProperties.CorrelationId,
                Type = response.GetType().FullName
            };

            await channel.BasicPublishAsync(string.Empty, ea.BasicProperties.ReplyTo, true, replyProps, responseBytes);
        }
    }

}
