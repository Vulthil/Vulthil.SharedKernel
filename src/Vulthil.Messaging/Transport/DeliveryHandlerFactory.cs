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
public sealed class DeliveryHandlerFactory : IMessageHandlerFactory<DeliveryHandler>
{
    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="consumerType"/> does not implement <c>IConsumer&lt;TMessage&gt;</c> for <paramref name="messageType"/>.</exception>
    public DeliveryHandler ForConsumer(Type consumerType, Type messageType, RetryPolicyDefinition? retryPolicy)
    {
        ArgumentNullException.ThrowIfNull(consumerType);
        ArgumentNullException.ThrowIfNull(messageType);

        var invoker = Invoker.Create(typeof(ConsumerInvoker<,>), consumerType, messageType);
        return new(DeliveryHandler.BuildIdentity(consumerType, messageType), HandlerKind.Consumer, retryPolicy, invoker.InvokeAsync);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="consumerType"/> does not implement <c>IRequestConsumer&lt;TRequest, TResponse&gt;</c> for <paramref name="requestType"/> and <paramref name="responseType"/>.</exception>
    public DeliveryHandler ForRequestConsumer(Type consumerType, Type requestType, Type responseType)
    {
        ArgumentNullException.ThrowIfNull(consumerType);
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(responseType);

        var invoker = Invoker.Create(typeof(RequestConsumerInvoker<,,>), consumerType, requestType, responseType);
        return new(DeliveryHandler.BuildIdentity(consumerType, requestType), HandlerKind.RequestConsumer, retryPolicy: null, invoker.InvokeAsync);
    }

    private static bool EndsTheDelivery(Exception exception, IDeliveryPort port)
        => exception is OperationCanceledException && port.CancellationToken.IsCancellationRequested;

    private abstract class Invoker
    {
        public abstract Task<DeliveryAttempt> InvokeAsync(IServiceProvider services, object message, IDeliveryPort port, int retryCount);

        // Closing the invoker over the registration's CLR types checks the generic constraints once, so a consumer
        // that does not match its message or response type fails at registration, and every attempt then runs typed
        // code with no reflection.
        public static Invoker Create(Type unboundInvoker, params Type[] typeArguments)
            => (Invoker)Activator.CreateInstance(unboundInvoker.MakeGenericType(typeArguments))!;
    }

    private sealed class ConsumerInvoker<TConsumer, TMessage> : Invoker
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : notnull
    {
        public override async Task<DeliveryAttempt> InvokeAsync(IServiceProvider services, object message, IDeliveryPort port, int retryCount)
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
                return consumed ? DeliveryAttempt.Consumed : DeliveryAttempt.NotConsumed;
            }
            catch (Exception exception) when (!EndsTheDelivery(exception, port))
            {
                return DeliveryAttempt.Failed(exception, faultPort => faultPort.PublishFaultAsync(CreateFault(context, exception)));
            }
        }

        private static Fault<TMessage> CreateFault(MessageContext<TMessage> context, Exception exception) => new()
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

    private sealed class RequestConsumerInvoker<TConsumer, TRequest, TResponse> : Invoker
        where TConsumer : class, IRequestConsumer<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : notnull
    {
        public override async Task<DeliveryAttempt> InvokeAsync(IServiceProvider services, object message, IDeliveryPort port, int retryCount)
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
                    : (RpcReply.ShortCircuited(provider, context.RequestId, context.CorrelationId), DeliveryAttempt.NotConsumed);
            }
            catch (Exception exception) when (!EndsTheDelivery(exception, port))
            {
                (reply, attempt) = (RpcReply.Fault(exception, provider, context.RequestId, context.CorrelationId), DeliveryAttempt.NotConsumed);
            }

            await port.SendReplyAsync(reply).ConfigureAwait(false);
            return attempt;
        }
    }
}
