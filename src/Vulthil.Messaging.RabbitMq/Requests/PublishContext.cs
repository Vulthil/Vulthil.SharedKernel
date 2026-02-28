using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed class PublishContext : IPublishContext
{
    internal Dictionary<string, object?> Headers { get; } = [];
    internal string? RoutingKey { get; private set; }
    internal string? CorrelationId { get; private set; }
    public string? MessageId { get; set; }
    public string? ConversationId { get => Headers.TryGetValue("ConversationId", out var value) && value is string conversationId ? conversationId : null; set => Headers["ConversationId"] = value; }
    public string? InitiatorId { get => Headers.TryGetValue("InitiatorId", out var value) && value is string initiatorId ? initiatorId : null; set => Headers["InitiatorId"] = value; }
    public Uri? SourceAddress { get => Headers.TryGetValue("SourceAddress", out var value) && value is string sourceAddress ? new Uri(sourceAddress) : null; set => Headers["SourceAddress"] = MapUriToString(value); }
    public Uri? DestinationAddress { get => Headers.TryGetValue("DestinationAddress", out var value) && value is string destinationAddress ? new Uri(destinationAddress) : null; set => Headers["DestinationAddress"] = MapUriToString(value); }
    public Uri? ResponseAddress { get => Headers.TryGetValue("ResponseAddress", out var value) && value is string responseAddress ? new Uri(responseAddress) : null; set => Headers["ResponseAddress"] = MapUriToString(value); }
    public Uri? FaultAddress { get => Headers.TryGetValue("FaultAddress", out var value) && value is string faultAddress ? new Uri(faultAddress) : null; set => Headers["FaultAddress"] = MapUriToString(value); }

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

    private static string? MapUriToString(Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        return uri.Scheme == "queue" ? uri.LocalPath.TrimStart('/') : uri.ToString();
    }

    public static string? ResolveRoutingKeyFromUri(Uri? uri)
    {
        if (uri == null)
        {
            return null;
        }

        if (uri.Scheme == "queue")
        {
            return uri.LocalPath.TrimStart('/');
        }

        if (uri.Scheme == "rabbitmq" || uri.Scheme == "amqp" || uri.Scheme == "amqps")
        {
            return uri.AbsolutePath.TrimStart('/');
        }

        return uri.ToString();
    }
}
