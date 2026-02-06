using RabbitMQ.Client;

namespace Vulthil.Messaging.RabbitMq;

internal static class ExchangeTypeMapper
{
    extension(MessagingExchangeType type)
    {
        public string ToRabbitExchangeType() => type switch
        {
            MessagingExchangeType.Topic => ExchangeType.Topic,
            MessagingExchangeType.Direct => ExchangeType.Direct,
            MessagingExchangeType.Headers => ExchangeType.Headers,
            MessagingExchangeType.Fanout => ExchangeType.Fanout,
            _ => ExchangeType.Topic
        };
    }
}
