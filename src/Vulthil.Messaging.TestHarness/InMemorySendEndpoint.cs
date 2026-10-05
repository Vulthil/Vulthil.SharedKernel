using System.Collections.Concurrent;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>In-memory transport send-endpoint provider: hands out in-memory endpoints cached per address.</summary>
internal sealed class InMemorySendEndpointProvider : ITransportSendEndpointProvider
{
    private readonly IMessageConfigurationProvider _provider;
    private readonly InMemoryTransport _transport;
    private readonly TestHarness _harness;
    private readonly ConcurrentDictionary<Uri, ITransportSendEndpoint> _endpoints = new();

    public InMemorySendEndpointProvider(IMessageConfigurationProvider provider, InMemoryTransport transport, TestHarness harness)
    {
        _provider = provider;
        _transport = transport;
        _harness = harness;
    }

    public ValueTask<ITransportSendEndpoint> GetSendEndpointAsync(Uri address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        var endpoint = _endpoints.GetOrAdd(address, a => new InMemorySendEndpoint(a, _provider, _transport, _harness));
        return ValueTask.FromResult(endpoint);
    }
}

/// <summary>In-memory <see cref="ITransportSendEndpoint"/>: captures every sent message, then dispatches it to consumers in-process.</summary>
internal sealed class InMemorySendEndpoint : ITransportSendEndpoint
{
    private readonly IMessageConfigurationProvider _provider;
    private readonly InMemoryTransport _transport;
    private readonly TestHarness _harness;

    public InMemorySendEndpoint(Uri address, IMessageConfigurationProvider provider, InMemoryTransport transport, TestHarness harness)
    {
        Address = address;
        _provider = provider;
        _transport = transport;
        _harness = harness;
    }

    public Uri Address { get; }

    public async Task SendAsync(object message, PublishContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var envelope = OutgoingEnvelope.Build(_provider, message, context);
        _harness.RecordSent(message, envelope);
        await _transport.DeliverAsync(envelope, cancellationToken).ConfigureAwait(false);
    }
}
