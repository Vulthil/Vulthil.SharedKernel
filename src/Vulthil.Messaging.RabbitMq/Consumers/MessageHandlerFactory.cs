using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Envelope;
using Vulthil.Messaging.RabbitMq.Requests;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// Builds <see cref="MessageHandler"/> instances from open-generic consumer/message type pairs.
/// The factory methods are the single source of truth for the dispatch closure shape; reflection-driven
/// callers in <see cref="MessageTypeCache"/> bind to these via typed delegates so signature drift fails at startup.
/// </summary>
internal static class MessageHandlerFactory
{
    /// <summary>
    /// Builds a handler for a one-way <see cref="IConsumer{TMessage}"/>.
    /// </summary>
    public static MessageHandler ForConsumer<TConsumer, TMessage>(RetryPolicyDefinition? retryPolicy)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : notnull
        => new()
        {
            RetryPolicy = retryPolicy,
            Kind = HandlerKind.Consumer,
            DispatchAsync = async (sp, message, ea, envelope, _, ct) =>
            {
                var consumer = sp.GetRequiredService<TConsumer>();
                var publisher = sp.GetRequiredService<IPublisher>();
                var sendEndpointProvider = sp.GetRequiredService<ISendEndpointProvider>();
                var context = envelope is null
                    ? MessageContext.CreateContext((TMessage)message, ea, publisher, sendEndpointProvider, ct)
                    : MessageContext.CreateContext((TMessage)message, ea, envelope, publisher, sendEndpointProvider, ct);

                var pipeline = ConsumePipelineFactory.Build<TMessage>(
                    sp,
                    terminal: c => consumer.ConsumeAsync(c, c.CancellationToken));

                await pipeline(context);
            }
        };

    /// <summary>
    /// Builds a handler for a request/reply <see cref="IRequestConsumer{TRequest, TResponse}"/>.
    /// </summary>
    public static MessageHandler ForRequestConsumer<TConsumer, TRequest, TResponse>(RetryPolicyDefinition? retryPolicy)
        where TConsumer : class, IRequestConsumer<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : notnull
        => new()
        {
            RetryPolicy = retryPolicy,
            Kind = HandlerKind.RequestConsumer,
            DispatchAsync = async (sp, message, ea, envelope, channel, ct) =>
            {
                var consumer = sp.GetRequiredService<TConsumer>();
                var publisher = sp.GetRequiredService<IPublisher>();
                var sendEndpointProvider = sp.GetRequiredService<ISendEndpointProvider>();
                var jsonOptions = sp.GetRequiredService<IMessageConfigurationProvider>().JsonSerializerOptions;
                var context = envelope is null
                    ? MessageContext.CreateContext((TRequest)message, ea, publisher, sendEndpointProvider, ct)
                    : MessageContext.CreateContext((TRequest)message, ea, envelope, publisher, sendEndpointProvider, ct);

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
                        var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response, jsonOptions);
                        messageResult = MessageResult.Success(responseBytes);
                    }
                }
                catch (Exception exception)
                {
                    messageResult = MessageResult.Failure(exception.Message);
                }

                await SendResponseAsync(ea, messageResult, channel, jsonOptions);
            }
        };

    private static async Task SendResponseAsync(BasicDeliverEventArgs ea, MessageResult response, IChannel channel, JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrEmpty(ea.BasicProperties.ReplyTo))
        {
            return;
        }

        var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response, jsonOptions);
        var replyProps = new BasicProperties
        {
            CorrelationId = ea.BasicProperties.CorrelationId,
            Type = response.GetType().FullName
        };

        await channel.BasicPublishAsync(string.Empty, ea.BasicProperties.ReplyTo, true, replyProps, responseBytes);
    }
}
