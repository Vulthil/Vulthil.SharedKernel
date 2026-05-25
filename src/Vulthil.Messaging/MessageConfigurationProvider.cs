using System.Text.Json;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

internal sealed class MessageConfigurationProvider(MessagingOptions options) : IMessageConfigurationProvider
{
    private readonly MessagingOptions _options = options;

    public MessageConfiguration GetMessageConfiguration(Type messageType)
        => _options.GetMessageConfiguration(messageType);

    public MessageConfiguration GetMessageConfiguration<TMessage>() where TMessage : class
        => _options.GetMessageConfiguration<TMessage>();

    public JsonSerializerOptions JsonSerializerOptions => _options.JsonSerializerOptions;
    public TimeSpan DefaultTimeout => _options.DefaultTimeout;
    public string FaultExchangeName => _options.FaultExchangeName;

    public IReadOnlyCollection<QueueDefinition> QueueDefinitions => _options.QueueDefinitions.Values;
}
