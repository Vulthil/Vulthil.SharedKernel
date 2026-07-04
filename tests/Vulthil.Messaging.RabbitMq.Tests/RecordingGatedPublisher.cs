using RabbitMQ.Client;
using Vulthil.Messaging.RabbitMq.Consumers;

namespace Vulthil.Messaging.RabbitMq.Tests;

/// <summary>
/// Test double for <see cref="GatedPublisher"/>: records every publish handed to handler dispatch so a test
/// can assert what was written — and where — without standing up a channel. Pass <see cref="PublishAsync"/>
/// wherever a <see cref="GatedPublisher"/> is expected.
/// </summary>
internal sealed class RecordingGatedPublisher
{
    private readonly List<RecordedPublish> _published = [];

    public IReadOnlyList<RecordedPublish> Published => _published;

    public Task PublishAsync(string exchange, string routingKey, bool mandatory, BasicProperties basicProperties, ReadOnlyMemory<byte> body)
    {
        _published.Add(new RecordedPublish(exchange, routingKey, mandatory, basicProperties, body));
        return Task.CompletedTask;
    }

    internal sealed record RecordedPublish(string Exchange, string RoutingKey, bool Mandatory, BasicProperties Properties, ReadOnlyMemory<byte> Body);
}
