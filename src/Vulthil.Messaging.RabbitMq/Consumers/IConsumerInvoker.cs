using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Requests;

namespace Vulthil.Messaging.RabbitMq.Consumers;


internal interface IConsumerInvoker
{
    string RoutingKey { get; }
    RetryPolicyDefinition? RetryPolicy { get; }
    Task InvokeAsync(IServiceProvider sp, object message, BasicDeliverEventArgs ea, CancellationToken ct);
}

internal static class ConsumePipelineFactory
{
    /// <summary>
    /// Composes the registered <see cref="IConsumeFilter{TMessage}"/> instances around a terminal
    /// delegate. The first filter resolved from DI becomes the outermost; the terminal delegate
    /// runs innermost.
    /// </summary>
    public static ConsumeDelegate<TMessage> Build<TMessage>(
        IServiceProvider sp,
        ConsumeDelegate<TMessage> terminal)
        where TMessage : notnull
    {
        var filters = sp.GetServices<IConsumeFilter<TMessage>>().ToArray();
        if (filters.Length == 0)
        {
            return terminal;
        }

        var pipeline = terminal;
        // Iterate in reverse so the first-registered filter ends up outermost.
        for (var i = filters.Length - 1; i >= 0; i--)
        {
            var filter = filters[i];
            var next = pipeline;
            pipeline = context => filter.ConsumeAsync(context, next);
        }

        return pipeline;
    }
}

internal sealed class ConsumerInvoker<TConsumer, TMessage>(string routingKey, RetryPolicyDefinition? retryPolicy) : IConsumerInvoker
    where TConsumer : class, IConsumer<TMessage>
    where TMessage : notnull
{
    /// <summary>
    /// Represents this member.
    /// </summary>
    public string RoutingKey => routingKey;

    /// <summary>
    /// Represents this member.
    /// </summary>
    public RetryPolicyDefinition? RetryPolicy => retryPolicy;

    /// <summary>
    /// Executes this member.
    /// </summary>
    public async Task InvokeAsync(
        IServiceProvider sp,
        object message,
        BasicDeliverEventArgs ea,
        CancellationToken ct)
    {
        var consumer = sp.GetRequiredService<TConsumer>();
        var publisher = sp.GetRequiredService<IPublisher>();
        var sendEndpointProvider = sp.GetRequiredService<ISendEndpointProvider>();
        var context = MessageContext.CreateContext((TMessage)message, ea, publisher, sendEndpointProvider, ct);

        var pipeline = ConsumePipelineFactory.Build<TMessage>(
            sp,
            terminal: c => consumer.ConsumeAsync(c, c.CancellationToken));

        await pipeline(context);
    }
}

internal interface IRpcInvoker
{
    string RoutingKey { get; }
    RetryPolicyDefinition? RetryPolicy { get; }
    Task InvokeAsync(IServiceProvider sp, object message, BasicDeliverEventArgs ea, IChannel channel, CancellationToken ct);
}

internal sealed class RpcInvoker<TConsumer, TRequest, TResponse>(string routingKey, RetryPolicyDefinition? retryPolicy) : IRpcInvoker
    where TConsumer : class, IRequestConsumer<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : notnull
{
    /// <summary>
    /// Represents this member.
    /// </summary>
    public string RoutingKey => routingKey;
    /// <summary>
    /// Represents this member.
    /// </summary>
    public RetryPolicyDefinition? RetryPolicy => retryPolicy;

    /// <summary>
    /// Executes this member.
    /// </summary>
    public async Task InvokeAsync(IServiceProvider sp, object message, BasicDeliverEventArgs ea, IChannel channel, CancellationToken ct)
    {
        var consumer = sp.GetRequiredService<TConsumer>();
        var publisher = sp.GetRequiredService<IPublisher>();
        var sendEndpointProvider = sp.GetRequiredService<ISendEndpointProvider>();
        var jsonOptions = sp.GetRequiredService<IMessageConfigurationProvider>().JsonSerializerOptions;
        var context = MessageContext.CreateContext((TRequest)message, ea, publisher, sendEndpointProvider, ct);

        MessageResult messageResult;
        try
        {
            // The terminal stage captures the consumer's response so any wrapping filters can
            // observe completion (e.g. for telemetry) before we serialize and publish it.
            TResponse response = default!;
            var responseProduced = false;

            var pipeline = ConsumePipelineFactory.Build<TRequest>(
                sp,
                terminal: async c =>
                {
                    response = await consumer.ConsumeAsync(c, c.CancellationToken);
                    responseProduced = true;
                });

            await pipeline(context);

            if (!responseProduced)
            {
                messageResult = MessageResult.Failure("Consume pipeline did not produce a response (a filter likely short-circuited the chain).");
            }
            else
            {
                var responseByteArray = JsonSerializer.SerializeToUtf8Bytes(response, jsonOptions);
                messageResult = MessageResult.Success(responseByteArray);
            }
        }
        catch (Exception exception)
        {
            messageResult = MessageResult.Failure(exception.Message);
        }

        await SendResponseAsync(ea, messageResult, channel, jsonOptions);
    }

    private static async Task SendResponseAsync(BasicDeliverEventArgs ea, MessageResult response, IChannel channel, JsonSerializerOptions jsonOptions)
    {
        // SEND RESPONSE
        if (!string.IsNullOrEmpty(ea.BasicProperties.ReplyTo))
        {
            var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response, jsonOptions);
            var replyProps = new BasicProperties
            {
                CorrelationId = ea.BasicProperties.CorrelationId,
                Type = response.GetType().FullName
            };

            await channel.BasicPublishAsync(string.Empty, ea.BasicProperties.ReplyTo, true, replyProps, responseBytes);
        }
    }

}
