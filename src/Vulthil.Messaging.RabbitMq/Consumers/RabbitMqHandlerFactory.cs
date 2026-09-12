using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// Builds the <see cref="MessageHandler"/> dispatch closures of the RabbitMQ transport. Each closure resolves the
/// consumer from the delivery scope, builds the receive context for the envelope or bare-JSON path and runs the
/// consume pipeline; a request consumer's closure also publishes the reply — or an <see cref="RpcFault"/> — through
/// the worker's <see cref="GatedPublisher"/>. The base class binds each registration's CLR types to these methods.
/// </summary>
internal sealed class RabbitMqHandlerFactory : MessageHandlerFactory<MessageHandler>
{
    protected override MessageHandler CreateConsumerHandler<TConsumer, TMessage>(RetryPolicyDefinition? retryPolicy)
        => new()
        {
            RetryPolicy = retryPolicy,
            Identity = MessageHandler.BuildIdentity(typeof(TConsumer), typeof(TMessage)),
            Kind = HandlerKind.Consumer,
            DispatchAsync = async (sp, message, ea, envelope, _, ct) =>
            {
                var consumer = sp.GetRequiredService<TConsumer>();
                var publisher = sp.GetRequiredService<IPublisher>();
                var sendEndpointProvider = sp.GetRequiredService<ISendEndpointProvider>();
                var context = envelope is null
                    ? MessageContextFactory.CreateContext((TMessage)message, ea, publisher, sendEndpointProvider, ct)
                    : MessageContextFactory.CreateContext((TMessage)message, ea, envelope, publisher, sendEndpointProvider, ct);

                var pipeline = ConsumePipelineFactory.Build<TMessage>(
                    sp,
                    terminal: c => consumer.ConsumeAsync(c, c.CancellationToken));

                await pipeline(context).ConfigureAwait(false);
            }
        };

    protected override MessageHandler CreateRequestConsumerHandler<TConsumer, TRequest, TResponse>(RetryPolicyDefinition? retryPolicy)
        => new()
        {
            RetryPolicy = retryPolicy,
            Identity = MessageHandler.BuildIdentity(typeof(TConsumer), typeof(TRequest)),
            Kind = HandlerKind.RequestConsumer,
            DispatchAsync = async (sp, message, ea, envelope, publishAsync, ct) =>
            {
                var consumer = sp.GetRequiredService<TConsumer>();
                var publisher = sp.GetRequiredService<IPublisher>();
                var sendEndpointProvider = sp.GetRequiredService<ISendEndpointProvider>();
                var provider = sp.GetRequiredService<IMessageConfigurationProvider>();
                var jsonOptions = provider.JsonSerializerOptions;
                var context = envelope is null
                    ? MessageContextFactory.CreateContext((TRequest)message, ea, publisher, sendEndpointProvider, ct)
                    : MessageContextFactory.CreateContext((TRequest)message, ea, envelope, publisher, sendEndpointProvider, ct);

                MessageEnvelope reply;
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
                            response = await consumer.ConsumeAsync(c, c.CancellationToken).ConfigureAwait(false);
                            responseProduced = true;
                        });

                    await pipeline(context).ConfigureAwait(false);

                    reply = responseProduced
                        ? BuildReply(provider.GetUrn(typeof(TResponse)), JsonSerializer.SerializeToElement(response, jsonOptions), ea, envelope)
                        : BuildFaultReply(
                            "Consume pipeline did not produce a response (a filter likely short-circuited the chain).",
                            typeof(InvalidOperationException).FullName!,
                            stackTrace: null,
                            jsonOptions,
                            ea,
                            envelope);
                }
                catch (Exception exception)
                {
                    reply = BuildFaultReply(exception.Message, exception.GetType().FullName ?? "Unknown", exception.StackTrace, jsonOptions, ea, envelope);
                }

                await SendResponseAsync(ea, reply, publishAsync, jsonOptions).ConfigureAwait(false);
            }
        };

    private static MessageEnvelope BuildReply(Uri messageType, JsonElement message, BasicDeliverEventArgs ea, MessageEnvelope? requestEnvelope)
        => new()
        {
            MessageId = Guid.CreateVersion7().ToString(),
            RequestId = ea.BasicProperties.CorrelationId,
            CorrelationId = requestEnvelope?.CorrelationId,
            MessageType = messageType,
            Message = message,
            SentTime = DateTimeOffset.UtcNow,
        };

    private static MessageEnvelope BuildFaultReply(
        string message,
        string exceptionType,
        string? stackTrace,
        JsonSerializerOptions jsonOptions,
        BasicDeliverEventArgs ea,
        MessageEnvelope? requestEnvelope)
    {
        var fault = new RpcFault
        {
            Message = message,
            ExceptionType = exceptionType,
            StackTrace = stackTrace,
            FaultedAt = DateTimeOffset.UtcNow,
        };
        return BuildReply(RpcFault.UrnUri, JsonSerializer.SerializeToElement(fault, jsonOptions), ea, requestEnvelope);
    }

    private static async Task SendResponseAsync(BasicDeliverEventArgs ea, MessageEnvelope reply, GatedPublisher publishAsync, JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrEmpty(ea.BasicProperties.ReplyTo))
        {
            return;
        }

        var body = JsonSerializer.SerializeToUtf8Bytes(reply, jsonOptions);
        var replyProps = new BasicProperties
        {
            CorrelationId = ea.BasicProperties.CorrelationId,
            Type = reply.MessageType.AbsoluteUri,
            ContentType = RabbitMqConstants.ContentType,
        };

        await publishAsync(string.Empty, ea.BasicProperties.ReplyTo, true, replyProps, body).ConfigureAwait(false);
    }
}
