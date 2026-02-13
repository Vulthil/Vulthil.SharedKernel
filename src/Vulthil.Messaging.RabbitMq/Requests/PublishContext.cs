using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed class PublishContext : IPublishContext
{
    internal Dictionary<string, object?> Headers { get; } = [];
    internal string? RoutingKey { get; private set; }
    internal string? CorrelationId { get; private set; }

    public void AddHeader(string key, object? value) => Headers[key] = value;
    public void AddHeaders(IDictionary<string, object?> headers)
    {
        foreach (var item in headers)
        {
            Headers[item.Key] = item.Value;
        }
    }
    public void SetRoutingKey(string routingKey) => RoutingKey = routingKey;
    public void SetCorrelationId(string correlationId) => CorrelationId = correlationId;
}
