using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// In-memory <see cref="ITransport"/>. Assembles the same <see cref="MessageExecutionRegistry{THandler}"/> a
/// broker transport would from the configured queues, then runs produced messages through the matching consumers
/// in-process with the shared <see cref="DeliveryDispatcher"/> — no broker. Dispatch is synchronous: a
/// publish/send/request completes only after every triggered consumer and stub has run.
/// </summary>
internal sealed class InMemoryTransport : ITransport
{
    private readonly IMessageConfigurationProvider _provider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DeliveryDispatcher _dispatcher;
    private readonly TestHarness _harness;
    private readonly MessageExecutionRegistry<DeliveryHandler> _registry;

    public InMemoryTransport(
        IMessageConfigurationProvider provider,
        IServiceScopeFactory scopeFactory,
        DeliveryDispatcher dispatcher,
        TestHarness harness)
    {
        _provider = provider;
        _scopeFactory = scopeFactory;
        _dispatcher = dispatcher;
        _harness = harness;
        _registry = new MessageExecutionRegistry<DeliveryHandler>(provider, new DeliveryHandlerFactory());

        // The plans are built eagerly from the configured queues so the harness works whether or not the host's
        // hosted services are started — a unit test can resolve IPublisher/ITestHarness and assert immediately.
        foreach (var queue in provider.QueueDefinitions)
        {
            _registry.RegisterQueue(queue);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Delivers a published or sent message: runs every registered one-way consumer for the message URN through the
    /// dispatcher, then any ad-hoc <see cref="ITestHarness.Handle{TMessage}"/> stub for that URN.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> ended the delivery.</exception>
    public async Task DeliverAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        var plan = _registry.GetPlanByUrn(envelope.MessageType);
        if (plan is not null)
        {
            var consumers = plan.Handlers.Where(handler => handler.Kind == HandlerKind.Consumer).ToList();
            await DispatchAsync(consumers, plan, envelope, cancellationToken).ConfigureAwait(false);
        }

        await RunStubsAsync(envelope, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Delivers a request and returns the reply envelope: a registered <see cref="ITestHarness.Respond{TRequest, TResponse}"/>
    /// responder takes precedence, then a real request consumer; returns <see langword="null"/> when neither exists.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> ended the delivery before the
    /// request consumer answered.</exception>
    public async Task<MessageEnvelope?> DeliverRequestAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        var responder = _harness.ResponderFor(envelope.MessageType);
        if (responder is not null)
        {
            var scope = _scopeFactory.CreateAsyncScope();
            await using var _ = scope.ConfigureAwait(false);
            return responder(scope.ServiceProvider, envelope, cancellationToken);
        }

        var plan = _registry.GetPlanByUrn(envelope.MessageType);
        var handler = plan?.Handlers.FirstOrDefault(h => h.Kind == HandlerKind.RequestConsumer);
        if (plan is null || handler is null)
        {
            return null;
        }

        var port = await DispatchAsync([handler], plan, envelope, cancellationToken).ConfigureAwait(false);
        return port.Reply;
    }

    private async Task<DeliveryPort> DispatchAsync(
        List<DeliveryHandler> handlers,
        MessageExecutionPlan<DeliveryHandler> plan,
        MessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var message = Deserialize(envelope, plan.MessageType.Type);
        var port = new DeliveryPort(this, envelope, cancellationToken);
        var outcome = await _dispatcher.DispatchAsync(handlers, message, port).ConfigureAwait(false);

        foreach (var _ in outcome.Consumed)
        {
            _harness.RecordConsumed(message, envelope);
        }

        if (outcome.Settlement == DeliverySettlement.Abandon)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        return port;
    }

    private async Task RunStubsAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        foreach (var stub in _harness.HandlersFor(envelope.MessageType))
        {
            var scope = _scopeFactory.CreateAsyncScope();
            await using var _ = scope.ConfigureAwait(false);
            await stub(scope.ServiceProvider, envelope, cancellationToken).ConfigureAwait(false);
        }
    }

    private object Deserialize(MessageEnvelope envelope, Type messageType)
        => envelope.Message.Deserialize(messageType, _provider.JsonSerializerOptions)
            ?? throw new InvalidOperationException($"The in-memory transport could not deserialize a '{envelope.MessageType}' payload.");

    /// <summary>
    /// The harness's <see cref="IDeliveryPort"/> for one delivery. Retries run in-process, a fault is captured as a
    /// published message and runs the <see cref="ITestHarness.Handle{TMessage}"/> stubs for it, and a request
    /// consumer's reply is kept for the requester.
    /// </summary>
    private sealed class DeliveryPort(InMemoryTransport transport, MessageEnvelope envelope, CancellationToken cancellationToken) : IDeliveryPort
    {
        public int RetryCount => 0;

        public CancellationToken CancellationToken => cancellationToken;

        public bool CanRedeliverLater => false;

        public MessageEnvelope? Reply { get; private set; }

        public MessageContext<TMessage> CreateContext<TMessage>(TMessage message, IServiceProvider services, int retryCount)
            where TMessage : notnull
            => InMemoryContext.Create(services, message, envelope, cancellationToken, retryCount);

        // Retries run back-to-back, so a test never waits out a policy's back-off.
        public Task WaitBeforeRetryAsync(TimeSpan delay)
            => cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;

        public Task PublishFaultAsync<TMessage>(Fault<TMessage> fault)
            where TMessage : notnull
        {
            var context = new PublishContext();
            if (envelope.CorrelationId is { } correlationId)
            {
                context.SetCorrelationId(correlationId);
            }

            var faultEnvelope = OutgoingEnvelope.Build(transport._provider, fault, context);
            transport._harness.RecordPublished(fault, faultEnvelope);
            return transport.RunStubsAsync(faultEnvelope, cancellationToken);
        }

        public Task SendReplyAsync(MessageEnvelope reply)
        {
            Reply = reply;
            return Task.CompletedTask;
        }
    }
}
