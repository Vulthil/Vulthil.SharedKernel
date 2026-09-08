using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq;

/// <summary>
/// Resolves Vulthil and AMQP address URIs to the broker routing key (queue name) they denote.
/// </summary>
internal static class RabbitMqAddress
{
    /// <summary>
    /// Maps an address URI to the routing key used to reach it: the queue name for <c>queue:</c> URIs (per
    /// <see cref="MessageAddress.QueueName"/>) and for AMQP (<c>rabbitmq:</c>/<c>amqp:</c>/<c>amqps:</c>) URIs, or
    /// the full string for anything else. Returns <see langword="null"/> when <paramref name="uri"/> is
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="uri">The address URI to resolve, or <see langword="null"/>.</param>
    /// <returns>The routing key, or <see langword="null"/> when <paramref name="uri"/> is <see langword="null"/>.</returns>
    public static string? ResolveRoutingKey(Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        return MessageAddress.QueueName(uri)
            ?? (IsAmqpScheme(uri.Scheme) ? uri.AbsolutePath.TrimStart('/') : uri.ToString());
    }

    private static bool IsAmqpScheme(string scheme) => scheme is "rabbitmq" or "amqp" or "amqps";
}
