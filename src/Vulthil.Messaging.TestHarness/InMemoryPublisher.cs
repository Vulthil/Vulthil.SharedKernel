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

    public async Task PublishAsync(object message, PublishContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var envelope = OutgoingEnvelope.Build(_provider, message, context);
        _harness.RecordPublished(message, envelope);
        await _transport.DeliverAsync(envelope, cancellationToken).ConfigureAwait(false);
    }
}
