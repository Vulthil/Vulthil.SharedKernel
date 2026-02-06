namespace Vulthil.Messaging.RabbitMq;

internal static class RabbitMqConstants
{
    public const string ContentType = "application/json";


    public static string? GetMetadata(Type type, object message, IReadOnlyDictionary<Type, Func<object, string>> registry)
    {
        var current = type;
        while (current != null && current != typeof(object))
        {
            if (registry.TryGetValue(current, out var picker))
            {
                return picker(message);
            }

            current = current.BaseType; // Walk up the inheritance tree
        }
        return null;
    }
}
