using System.Text.Json;

namespace Vulthil.Messaging;

internal sealed class MessageConfigurationProvider : IMessageConfigurationProvider
{
    private readonly MessagingOptions _options;

    public MessageConfigurationProvider(MessagingOptions options)
    {
        _options = options;
    }

    public MessageConfiguration GetMessageConfiguration(Type messageType)
        => _options.GetMessageConfiguration(messageType);

    public MessageConfiguration<TMessage> GetMessageConfiguration<TMessage>() where TMessage : class
        => (MessageConfiguration<TMessage>)_options.GetMessageConfiguration<TMessage>();

    public JsonSerializerOptions JsonSerializerOptions => _options.JsonSerializerOptions;
    public TimeSpan DefaultTimeout => _options.DefaultTimeout;
}
