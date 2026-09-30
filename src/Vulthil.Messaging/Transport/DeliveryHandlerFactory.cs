using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Builds the <see cref="DeliveryHandler"/> for each consumer registration, so a transport's
/// <see cref="MessageExecutionRegistry{THandler}"/> holds handlers a <see cref="DeliveryDispatcher"/> can run. Each
/// attempt resolves the consumer from the attempt's scope, builds the receive context through the
/// <see cref="IDeliveryPort"/>, and runs the consume pipeline; a request consumer's attempt then sends its
/// <see cref="RpcReply"/> through the port.
/// </summary>
public sealed class DeliveryHandlerFactory : MessageHandlerFactory<DeliveryHandler>
{
    /// <inheritdoc />
    protected override DeliveryHandler CreateConsumerHandler<TConsumer, TMessage>(RetryPolicyDefinition? retryPolicy)
        => new(DeliveryHandler.BuildIdentity(typeof(TConsumer), typeof(TMessage)), HandlerKind.Consumer, retryPolicy, ConsumeAsync<TConsumer, TMessage>);

    /// <inheritdoc />
    /// <remarks>
    /// The handler ignores <paramref name="retryPolicy"/>: a request consumer never retries, and replies with an
    /// <see cref="RpcFault"/> instead.
    /// </remarks>
    protected override DeliveryHandler CreateRequestConsumerHandler<TConsumer, TRequest, TResponse>(RetryPolicyDefinition? retryPolicy)
        => new(DeliveryHandler.BuildIdentity(typeof(TConsumer), typeof(TRequest)), HandlerKind.RequestConsumer, retryPolicy: null, AnswerAsync<TConsumer, TRequest, TResponse>);

    private static async Task<DeliveryAttempt> ConsumeAsync<TConsumer, TMessage>(IServiceProvider services, object message, IDeliveryPort port, int retryCount)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : notnull
    {
        var context = port.CreateContext((TMessage)message, services, retryCount);
        try
        {
            var consumer = services.GetRequiredService<TConsumer>();
            var consumed = false;
            var pipeline = ConsumePipelineFactory.Build<TMessage>(services, terminal: async c =>
            {
                await consumer.ConsumeAsync(c, c.CancellationToken).ConfigureAwait(false);
                consumed = true;
            });
            await pipeline(context).ConfigureAwait(false);
            return consumed ? DeliveryAttempt.Consumed : DeliveryAttempt.ShortCircuited;
        }
        catch (Exception exception) when (!EndsTheDelivery(exception, port))
        {
            return DeliveryAttempt.Failed(exception, faultPort => faultPort.PublishFaultAsync(CreateFault(context, exception)));
        }
    }

    private static async Task<DeliveryAttempt> AnswerAsync<TConsumer, TRequest, TResponse>(IServiceProvider services, object message, IDeliveryPort port, int retryCount)
        where TConsumer : class, IRequestConsumer<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : notnull
    {
        var provider = services.GetRequiredService<IMessageConfigurationProvider>();
        var context = port.CreateContext((TRequest)message, services, retryCount);

        MessageEnvelope reply;
        DeliveryAttempt attempt;
        try
        {
            var consumer = services.GetRequiredService<TConsumer>();

            // The terminal stage captures the response so any wrapping filters observe completion (for example for
            // telemetry) before the reply is built and sent.
            TResponse response = default!;
            var responseProduced = false;
            var pipeline = ConsumePipelineFactory.Build<TRequest>(services, terminal: async c =>
            {
                response = await consumer.ConsumeAsync(c, c.CancellationToken).ConfigureAwait(false);
                responseProduced = true;
            });
            await pipeline(context).ConfigureAwait(false);

            (reply, attempt) = responseProduced
                ? (RpcReply.Success(response, provider, context.RequestId, context.CorrelationId), DeliveryAttempt.Consumed)
                : (RpcReply.ShortCircuited(provider, context.RequestId, context.CorrelationId), DeliveryAttempt.AnsweredWithFault);
        }
        catch (Exception exception) when (!EndsTheDelivery(exception, port))
        {
            (reply, attempt) = (RpcReply.Fault(exception, provider, context.RequestId, context.CorrelationId), DeliveryAttempt.AnsweredWithFault);
        }

        await port.SendReplyAsync(reply).ConfigureAwait(false);
        return attempt;
    }

    private static bool EndsTheDelivery(Exception exception, IDeliveryPort port)
        => exception is OperationCanceledException && port.CancellationToken.IsCancellationRequested;

    private static Fault<TMessage> CreateFault<TMessage>(MessageContext<TMessage> context, Exception exception)
        where TMessage : notnull
        => new()
        {
            Message = context.Message,
            ExceptionMessage = exception.Message,
            StackTrace = exception.StackTrace,
            ExceptionType = exception.GetType().FullName ?? "Unknown",
            FaultedAt = DateTimeOffset.UtcNow,
            OriginalContext = new MessageContextSnapshot
            {
                MessageId = context.MessageId,
                RequestId = context.RequestId,
                CorrelationId = context.CorrelationId,
                ConversationId = context.ConversationId,
                InitiatorId = context.InitiatorId,
                SourceAddress = context.SourceAddress,
                DestinationAddress = context.DestinationAddress,
                ResponseAddress = context.ResponseAddress,
                FaultAddress = context.FaultAddress,
                RoutingKey = context.RoutingKey,
                RetryCount = context.RetryCount,
            },
        };
}
