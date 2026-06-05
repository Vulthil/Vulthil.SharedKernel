using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>Runs a registered ad-hoc stub for a delivered message of a known type.</summary>
internal delegate Task AdHocHandler(IServiceProvider scope, MessageEnvelope envelope, CancellationToken cancellationToken);

/// <summary>Runs a registered ad-hoc responder for a delivered request, returning the reply envelope.</summary>
internal delegate MessageEnvelope AdHocResponder(IServiceProvider scope, MessageEnvelope envelope, CancellationToken cancellationToken);

/// <summary>
/// Default <see cref="ITestHarness"/>: a thread-safe in-memory log of produced/consumed messages plus the
/// ad-hoc stubs registered through <see cref="Handle{TMessage}"/> and <see cref="Respond{TRequest, TResponse}"/>.
/// Stubs are keyed by the message wire URN so the in-memory transport can match a delivery without a CLR type.
/// </summary>
internal sealed class TestHarness : ITestHarness
{
    private readonly IMessageConfigurationProvider _provider;

    private readonly ConcurrentQueue<RecordedMessage> _published = new();
    private readonly ConcurrentQueue<RecordedMessage> _sent = new();
    private readonly ConcurrentQueue<RecordedMessage> _consumed = new();
    private readonly ConcurrentQueue<RecordedMessage> _requested = new();

    private readonly ConcurrentDictionary<Uri, ImmutableList<AdHocHandler>> _handlers = new();
    private readonly ConcurrentDictionary<Uri, AdHocResponder> _responders = new();

    public TestHarness(IMessageConfigurationProvider provider) => _provider = provider;

    public IReadOnlyList<CapturedMessage<TMessage>> Published<TMessage>() where TMessage : notnull => Project<TMessage>(_published);
    public IReadOnlyList<CapturedMessage<TMessage>> Sent<TMessage>() where TMessage : notnull => Project<TMessage>(_sent);
    public IReadOnlyList<CapturedMessage<TMessage>> Consumed<TMessage>() where TMessage : notnull => Project<TMessage>(_consumed);
    public IReadOnlyList<CapturedMessage<TMessage>> Requested<TMessage>() where TMessage : notnull => Project<TMessage>(_requested);

    public void Handle<TMessage>(Func<IMessageContext<TMessage>, Task> handler) where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);
        var built = BuildHandler(handler);
        var urn = _provider.GetUrn(typeof(TMessage));
        _handlers.AddOrUpdate(urn, _ => [built], (_, existing) => existing.Add(built));
    }

    public void Respond<TRequest, TResponse>(Func<IMessageContext<TRequest>, TResponse> responder)
        where TRequest : notnull
        where TResponse : notnull
    {
        ArgumentNullException.ThrowIfNull(responder);
        _responders[_provider.GetUrn(typeof(TRequest))] = BuildResponder(responder);
    }

    public void Clear()
    {
        _published.Clear();
        _sent.Clear();
        _consumed.Clear();
        _requested.Clear();
    }

    internal void RecordPublished(object message, MessageEnvelope envelope) => _published.Enqueue(new RecordedMessage(message, envelope));
    internal void RecordSent(object message, MessageEnvelope envelope) => _sent.Enqueue(new RecordedMessage(message, envelope));
    internal void RecordConsumed(object message, MessageEnvelope envelope) => _consumed.Enqueue(new RecordedMessage(message, envelope));
    internal void RecordRequested(object message, MessageEnvelope envelope) => _requested.Enqueue(new RecordedMessage(message, envelope));

    internal IReadOnlyList<AdHocHandler> HandlersFor(Uri urn)
        => _handlers.TryGetValue(urn, out var handlers) ? handlers : [];

    internal AdHocResponder? ResponderFor(Uri urn) => _responders.GetValueOrDefault(urn);

    private static AdHocHandler BuildHandler<TMessage>(Func<IMessageContext<TMessage>, Task> handler) where TMessage : notnull
        => (scope, envelope, ct) =>
        {
            var message = InMemoryContext.Deserialize<TMessage>(scope, envelope);
            var context = InMemoryContext.Create(scope, message, envelope, ct);
            return handler(context);
        };

    private static AdHocResponder BuildResponder<TRequest, TResponse>(Func<IMessageContext<TRequest>, TResponse> responder)
        where TRequest : notnull
        where TResponse : notnull
        => (scope, envelope, ct) =>
        {
            var provider = scope.GetRequiredService<IMessageConfigurationProvider>();
            var options = provider.JsonSerializerOptions;
            var request = InMemoryContext.Deserialize<TRequest>(scope, envelope);
            var context = InMemoryContext.Create(scope, request, envelope, ct);

            try
            {
                var response = responder(context);
                return InMemoryReply.Build(provider.GetUrn(typeof(TResponse)), JsonSerializer.SerializeToElement(response, options), envelope);
            }
            catch (Exception ex)
            {
                return InMemoryReply.BuildFault(ex, options, envelope);
            }
        };

    private static List<CapturedMessage<TMessage>> Project<TMessage>(ConcurrentQueue<RecordedMessage> log) where TMessage : notnull
        => log.Where(entry => entry.Message is TMessage)
            .Select(entry => new CapturedMessage<TMessage>((TMessage)entry.Message, entry.Envelope))
            .ToList();

    private sealed record RecordedMessage(object Message, MessageEnvelope Envelope);
}
