namespace Vulthil.Messaging.Transport;

/// <summary>
/// The address rules every producer and consumer path shares: how an address carried as a string (an envelope field,
/// a header value, a reply-to queue) becomes a <see cref="Uri"/>, how a <c>queue:</c> address names its queue, and how
/// an address is written into a header. A bare name denotes a queue; applying that rule on both sides is what lets a
/// reply-to queue name, a header value and a <c>queue:</c> URI all denote the same destination.
/// </summary>
public static class MessageAddress
{
    private const string QueueScheme = "queue";

    /// <summary>
    /// Parses an address carried as a string. An absolute URI is returned as is; a bare name denotes a queue and
    /// becomes <c>queue:&lt;name&gt;</c>; a <see langword="null"/> or blank value is no address.
    /// </summary>
    /// <param name="value">The address string, or <see langword="null"/>.</param>
    /// <returns>The address, or <see langword="null"/> when <paramref name="value"/> is <see langword="null"/> or blank.</returns>
    public static Uri? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : Queue(value);
    }

    /// <summary>
    /// Creates the <c>queue:</c> address that denotes <paramref name="queueName"/>.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    /// <returns>The <c>queue:&lt;name&gt;</c> address.</returns>
    public static Uri Queue(string queueName)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        return new Uri($"{QueueScheme}:{queueName}");
    }

    /// <summary>
    /// Returns the queue name a <c>queue:</c> address denotes, or <see langword="null"/> for an address of any other
    /// scheme.
    /// </summary>
    /// <param name="address">The address to inspect.</param>
    /// <returns>The queue name, or <see langword="null"/> when <paramref name="address"/> is not a <c>queue:</c> address.</returns>
    public static string? QueueName(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.Scheme == QueueScheme ? address.LocalPath.TrimStart('/') : null;
    }

    /// <summary>
    /// Formats an address for a header: a <c>queue:</c> address is stored as its bare queue name, any other address as
    /// its full URI string. <see cref="Parse"/> reverses it.
    /// </summary>
    /// <param name="address">The address to format.</param>
    /// <returns>The header value.</returns>
    public static string ToHeaderValue(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return QueueName(address) ?? address.ToString();
    }
}
