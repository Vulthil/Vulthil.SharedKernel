using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// Open-generic builders for in-memory dispatch handlers. The methods are the single source of truth for the
/// dispatch closure shape; <see cref="InMemoryHandlerFactory"/> binds to them via typed delegates, so signature
/// drift fails at startup. Public on an internal class so reflection binds with <c>BindingFlags.Public</c>.
/// </summary>
internal static class InMemoryMessageHandlers
{
    /// <summary>
    /// Builds a handler for a one-way <see cref="IConsumer{TMessage}"/>. A throwing consumer is retried in-process
    /// per <paramref name="retryPolicy"/> — the registration's effective policy (per-consumer, or the queue default),
    /// resolved by the registry — with a fresh scope per attempt, mirroring the broker transport but without the real
    /// back-off delays. Retries are per handler: another consumer of the same message runs its own closure, so one
    /// consumer's failure never re-runs a consumer that already succeeded. Once the attempts are exhausted a
    /// <see cref="Fault{TMessage}"/> is published and the delivery completes normally, so the originating
    /// publish/send succeeds just as it would against a real broker.
    /// </summary>
    public static InMemoryHandler ForConsumer<TConsumer, TMessage>(RetryPolicyDefinition? retryPolicy)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : notnull
        => new(HandlerKind.Consumer, async (scope, message, envelope, cancellationToken) =>
        {
            var scopeFactory = scope.GetRequiredService<IServiceScopeFactory>();
            var harness = scope.GetRequiredService<TestHarness>();
            var maxRetries = Math.Max(0, retryPolicy?.MaxRetryCount ?? 0);
            var ignoredExceptions = retryPolicy?.GetIgnoredExceptionTypes();

            Exception? lastError = null;
            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                await using var attemptScope = scopeFactory.CreateAsyncScope();
                var serviceProvider = attemptScope.ServiceProvider;
                var consumer = serviceProvider.GetRequiredService<TConsumer>();
                var context = InMemoryContext.Create(serviceProvider, (TMessage)message, envelope, cancellationToken, attempt);

                try
                {
                    var pipeline = ConsumePipelineFactory.Build<TMessage>(serviceProvider, terminal: async c =>
                    {
                        await consumer.ConsumeAsync(c, c.CancellationToken);
                        harness.RecordConsumed((TMessage)message, envelope);
                    });

                    await pipeline(context);
                    return null;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lastError = ex;
                    if (ignoredExceptions is not null && ignoredExceptions.Contains(ex.GetType()))
                    {
                        break;
                    }
                }
            }

            await PublishFaultAsync<TMessage>(scope, (TMessage)message, envelope, lastError!, maxRetries, cancellationToken);
            return null;
        });

    /// <summary>Builds a handler for a request/reply <see cref="IRequestConsumer{TRequest, TResponse}"/>.</summary>
    public static InMemoryHandler ForRequestConsumer<TConsumer, TRequest, TResponse>()
        where TConsumer : class, IRequestConsumer<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : notnull
        => new(HandlerKind.RequestConsumer, async (scope, message, envelope, ct) =>
        {
            var consumer = scope.GetRequiredService<TConsumer>();
            var provider = scope.GetRequiredService<IMessageConfigurationProvider>();
            var harness = scope.GetRequiredService<TestHarness>();
            var options = provider.JsonSerializerOptions;
            var context = InMemoryContext.Create(scope, (TRequest)message, envelope, ct);

            try
            {
                TResponse response = default!;
                var produced = false;

                var pipeline = ConsumePipelineFactory.Build<TRequest>(scope, terminal: async c =>
                {
                    harness.RecordConsumed((TRequest)message, envelope);
                    response = await consumer.ConsumeAsync(c, c.CancellationToken);
                    produced = true;
                });

                await pipeline(context);

                return (MessageEnvelope?)(produced
                    ? InMemoryReply.Build(provider.GetUrn(typeof(TResponse)), JsonSerializer.SerializeToElement(response, options), envelope)
                    : InMemoryReply.BuildFault(
                        "Consume pipeline did not produce a response (a filter likely short-circuited the chain).",
                        typeof(InvalidOperationException).FullName!,
                        stackTrace: null,
                        options,
                        envelope));
            }
            catch (Exception ex)
            {
                return InMemoryReply.BuildFault(ex, options, envelope);
            }
        });

    /// <summary>
    /// Publishes a <see cref="Fault{TMessage}"/> for a terminally-failed one-way delivery, mirroring the broker
    /// transport: the fault is captured (so tests can assert it) and delivered in-process to any consumer bound to
    /// it. Best-effort — it never disrupts completing the original delivery.
    /// </summary>
    private static async Task PublishFaultAsync<TMessage>(
        IServiceProvider scope,
        TMessage message,
        MessageEnvelope envelope,
        Exception error,
        int retryCount,
        CancellationToken cancellationToken)
        where TMessage : notnull
    {
        var provider = scope.GetRequiredService<IMessageConfigurationProvider>();
        var transport = scope.GetRequiredService<InMemoryTransport>();
        var harness = scope.GetRequiredService<TestHarness>();
        var context = InMemoryContext.Create(scope, message, envelope, cancellationToken, retryCount);

        var fault = new Fault<TMessage>
        {
            Message = message,
            ExceptionMessage = error.Message,
            StackTrace = error.StackTrace,
            ExceptionType = error.GetType().FullName ?? "Unknown",
            FaultedAt = DateTimeOffset.UtcNow,
            OriginalContext = CreateSnapshot(context),
        };

        var faultEnvelope = OutgoingEnvelope.Build(provider, fault, new PublishContext());
        harness.RecordPublished(fault, faultEnvelope);
        await transport.DeliverAsync(faultEnvelope, cancellationToken);
    }

    private static MessageContextSnapshot CreateSnapshot<TMessage>(MessageContext<TMessage> context)
        where TMessage : notnull
        => new()
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
        };
}
