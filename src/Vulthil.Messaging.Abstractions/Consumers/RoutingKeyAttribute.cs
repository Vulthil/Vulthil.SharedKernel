namespace Vulthil.Messaging.Abstractions.Consumers;

/// <summary>
/// Specifies a routing key pattern to use when binding a consumer to an exchange.
/// </summary>
/// <param name="pattern">The routing key pattern (e.g., "#", "Order.*").</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RoutingKeyAttribute(string pattern) : Attribute
{
    /// <summary>
    /// Gets the routing key pattern used for exchange binding (e.g., "#", "Order.*").
    /// </summary>
    public string Pattern { get; } = pattern;
}
