namespace Vulthil.Messaging.Transport;

/// <summary>
/// Identifies whether an outgoing message is being broadcast over an exchange (publish) or delivered to a single
/// addressable destination (send).
/// </summary>
public enum PublishKind
{
    /// <summary>A publish/subscribe broadcast to an exchange.</summary>
    Publish,

    /// <summary>A point-to-point send to a specific endpoint address.</summary>
    Send,
}
