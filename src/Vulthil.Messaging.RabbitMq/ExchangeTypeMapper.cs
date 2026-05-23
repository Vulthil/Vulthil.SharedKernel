using RabbitMQ.Client;

namespace Vulthil.Messaging.RabbitMq;

internal static class ExchangeTypeMapper
{
#if NET10_0_OR_GREATER
    extension(MessagingExchangeType type)
    {
        /// <summary>
        /// Executes this member.
        /// </summary>
        public string ToRabbitExchangeType() => type switch
        {
            MessagingExchangeType.Topic => ExchangeType.Topic,
            MessagingExchangeType.Direct => ExchangeType.Direct,
            MessagingExchangeType.Headers => ExchangeType.Headers,
            MessagingExchangeType.Fanout => ExchangeType.Fanout,
            _ => ExchangeType.Topic
        };
    }
#else
    public static string ToRabbitExchangeType(this MessagingExchangeType type) => type switch
    {
        MessagingExchangeType.Topic => ExchangeType.Topic,
        MessagingExchangeType.Direct => ExchangeType.Direct,
        MessagingExchangeType.Headers => ExchangeType.Headers,
        MessagingExchangeType.Fanout => ExchangeType.Fanout,
        _ => ExchangeType.Topic
    };
#endif
}
