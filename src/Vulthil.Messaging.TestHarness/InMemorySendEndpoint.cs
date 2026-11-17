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
        var endpoint = _endpoints.GetOrAdd(address, CreateEndpoint);
        return ValueTask.FromResult(endpoint);
    }

    private ITransportSendEndpoint CreateEndpoint(Uri address)
    {
        // The broker transport resolves a send address to its queue by the same rules, and rejects an address that
        // names no queue, so the harness routes a send to the queue the broker would.
        var queueName = MessageAddress.QueueName(address)
            ?? (address.Scheme is "rabbitmq" or "amqp" or "amqps" ? address.AbsolutePath.TrimStart('/') : address.ToString());
        if (string.IsNullOrEmpty(queueName))
        {
            throw new ArgumentException(
                $"Send endpoint address '{address}' does not name a destination queue. " +
                "Use a 'queue:<name>' URI or an absolute amqp/rabbitmq URI whose path names the queue.",
                nameof(address));
        }

        return new InMemorySendEndpoint(address, queueName, _provider, _transport, _harness);
    }
}

/// <summary>
/// In-memory <see cref="ITransportSendEndpoint"/>: captures every sent message with its address, then dispatches it
/// in-process to the consumers of the queue that the address names.
/// </summary>
internal sealed class InMemorySendEndpoint : ITransportSendEndpoint
{
    private readonly string _queueName;
    private readonly IMessageConfigurationProvider _provider;
    private readonly InMemoryTransport _transport;
    private readonly TestHarness _harness;

    public InMemorySendEndpoint(Uri address, string queueName, IMessageConfigurationProvider provider, InMemoryTransport transport, TestHarness harness)
    {
        Address = address;
        _queueName = queueName;
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
        _harness.RecordSent(message, envelope, Address);
        await _transport.DeliverSentAsync(envelope, _queueName, cancellationToken).ConfigureAwait(false);
    }
}
