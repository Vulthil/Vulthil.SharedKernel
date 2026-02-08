namespace Vulthil.Messaging.Abstractions.Consumers;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RoutingKeyAttribute(string pattern) : Attribute
{
    public string Pattern { get; } = pattern;
}
