using RabbitMQ.Client;

namespace Vulthil.Messaging.RabbitMq;

internal static class ExchangeTypeMapper
{
    extension(MessagingExchangeType type)
    {
        /// <summary>
        /// Maps this <see cref="MessagingExchangeType"/> to the corresponding RabbitMQ exchange type string,
        /// falling back to <see cref="ExchangeType.Topic"/> for unrecognized values.
        /// </summary>
        /// <returns>The RabbitMQ exchange type name.</returns>
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
