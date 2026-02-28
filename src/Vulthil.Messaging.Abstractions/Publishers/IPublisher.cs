namespace Vulthil.Messaging.Abstractions.Publishers;

public interface IPublisher
{
    Task PublishAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, Task>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TMessage : notnull;
}

public interface IPublishContext
{
    void SetRoutingKey(string routingKey);
    void SetCorrelationId(string correlationId);
    void AddHeader(string key, object? value);
    void AddHeaders(IDictionary<string, object?> headers);
    string? MessageId { get; set; }
    string? ConversationId { get; set; }
    string? InitiatorId { get; set; }
    Uri? ResponseAddress { get; set; }
    Uri? FaultAddress { get; set; }
}
