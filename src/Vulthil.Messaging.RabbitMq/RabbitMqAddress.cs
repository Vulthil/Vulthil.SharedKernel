namespace Vulthil.Messaging.RabbitMq;

/// <summary>
/// Resolves Vulthil and AMQP address URIs to the broker routing key (queue name) they denote.
/// </summary>
internal static class RabbitMqAddress
{
    /// <summary>
    /// Maps an address URI to the routing key used to reach it: the queue name for <c>queue:</c> and
    /// AMQP (<c>rabbitmq:</c>/<c>amqp:</c>/<c>amqps:</c>) URIs, or the full string for anything else.
    /// Returns <see langword="null"/> when <paramref name="uri"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="uri">The address URI to resolve, or <see langword="null"/>.</param>
    /// <returns>The routing key, or <see langword="null"/> when <paramref name="uri"/> is <see langword="null"/>.</returns>
    public static string? ResolveRoutingKey(Uri? uri)
    {
        if (uri is null)
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
