using System.Collections.Concurrent;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>In-memory transport send-endpoint provider: hands out in-memory endpoints cached per address.</summary>
internal sealed class InMemorySendEndpointProvider : ITransportSendEndpointProvider
{
    private readonly IMessageConfigurationProvider _provider;
    private readonly InMemoryTransport _transport;
    private readonly TestHarness _harness;
    private readonly ConcurrentDictionary<Uri, ISendEndpoint> _endpoints = new();

    public InMemorySendEndpointProvider(IMessageConfigurationProvider provider, InMemoryTransport transport, TestHarness harness)
    {
        _provider = provider;
        _transport = transport;
        _harness = harness;
    }

    public ValueTask<ISendEndpoint> GetSendEndpointAsync(Uri address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        var endpoint = _endpoints.GetOrAdd(address, a => new InMemorySendEndpoint(a, _provider, _transport, _harness));
        return ValueTask.FromResult(endpoint);
    }
}

/// <summary>In-memory <see cref="ISendEndpoint"/>: captures every sent message, then dispatches it to consumers in-process.</summary>
internal sealed class InMemorySendEndpoint : ISendEndpoint
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

    public Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : notnull
        => SendAsync(message, null, cancellationToken);

    public async Task SendAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = new PublishContext();
        if (configureContext is not null)
        {
            await configureContext(context).ConfigureAwait(false);
        }

        var envelope = OutgoingEnvelope.Build(_provider, message, context);
        _harness.RecordSent(message, envelope);
        await _transport.DeliverAsync(envelope, cancellationToken).ConfigureAwait(false);
    }
}
