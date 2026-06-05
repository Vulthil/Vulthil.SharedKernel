using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// In-memory <see cref="ITransport"/>. Assembles the same <see cref="MessageExecutionRegistry{THandler}"/> a
/// broker transport would from the configured queues, then dispatches produced messages to the matching
/// consumers in-process — no broker. Dispatch is synchronous: a publish/send/request completes only after every
/// triggered consumer and stub has run.
/// </summary>
internal sealed class InMemoryTransport : ITransport
{
    private readonly IMessageConfigurationProvider _provider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TestHarness _harness;
    private readonly MessageExecutionRegistry<InMemoryHandler> _registry;

    public InMemoryTransport(IMessageConfigurationProvider provider, IServiceScopeFactory scopeFactory, TestHarness harness)
    {
        _provider = provider;
        _scopeFactory = scopeFactory;
        _harness = harness;
        _registry = new MessageExecutionRegistry<InMemoryHandler>(provider, new InMemoryHandlerFactory());

        // The plans are built eagerly from the configured queues so the harness works whether or not the host's
        // hosted services are started — a unit test can resolve IPublisher/ITestHarness and assert immediately.
        foreach (var queue in provider.QueueDefinitions)
        {
            _registry.RegisterQueue(queue);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Delivers a published or sent message: runs every registered one-way consumer for the message URN, then any
    /// ad-hoc <see cref="ITestHarness.Handle{TMessage}"/> stub for that URN, all in one scope.
    /// </summary>
    public async Task DeliverAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var serviceProvider = scope.ServiceProvider;

        var plan = _registry.GetPlanByUrn(envelope.MessageType);
        if (plan is not null)
        {
            var message = Deserialize(envelope, plan.MessageType.Type);
            foreach (var handler in plan.Handlers.Where(handler => handler.Kind == HandlerKind.Consumer))
            {
                await handler.Dispatch(serviceProvider, message, envelope, cancellationToken);
            }
        }

        foreach (var stub in _harness.HandlersFor(envelope.MessageType))
        {
            await stub(serviceProvider, envelope, cancellationToken);
        }
    }

    /// <summary>
    /// Delivers a request and returns the reply envelope: a registered <see cref="ITestHarness.Respond{TRequest, TResponse}"/>
    /// responder takes precedence, then a real request consumer; returns <see langword="null"/> when neither exists.
    /// </summary>
    public async Task<MessageEnvelope?> DeliverRequestAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var serviceProvider = scope.ServiceProvider;

        var responder = _harness.ResponderFor(envelope.MessageType);
        if (responder is not null)
        {
            return responder(serviceProvider, envelope, cancellationToken);
        }

        var plan = _registry.GetPlanByUrn(envelope.MessageType);
        var handler = plan?.Handlers.FirstOrDefault(h => h.Kind == HandlerKind.RequestConsumer);
        if (plan is null || handler is null)
        {
            return null;
        }

        var message = Deserialize(envelope, plan.MessageType.Type);
        return await handler.Dispatch(serviceProvider, message, envelope, cancellationToken);
    }

    private object Deserialize(MessageEnvelope envelope, Type messageType)
        => envelope.Message.Deserialize(messageType, _provider.JsonSerializerOptions)
            ?? throw new InvalidOperationException($"The in-memory transport could not deserialize a '{envelope.MessageType}' payload.");
}
