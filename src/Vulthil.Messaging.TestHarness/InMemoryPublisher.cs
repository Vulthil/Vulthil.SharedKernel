using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>In-memory transport publisher: captures every published message, then dispatches it to consumers in-process.</summary>
internal sealed class InMemoryPublisher : ITransportPublisher
{
    private readonly IMessageConfigurationProvider _provider;
    private readonly InMemoryTransport _transport;
    private readonly TestHarness _harness;

    public InMemoryPublisher(IMessageConfigurationProvider provider, InMemoryTransport transport, TestHarness harness)
    {
        _provider = provider;
        _transport = transport;
        _harness = harness;
    }

    public async Task PublishAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = new PublishContext();
        if (configureContext is not null)
        {
            await configureContext(context);
        }

        var envelope = OutgoingEnvelope.Build(_provider, message, context);
        _harness.RecordPublished(message, envelope);
        await _transport.DeliverAsync(envelope, cancellationToken);
    }
}
