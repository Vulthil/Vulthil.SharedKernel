namespace Vulthil.Messaging;


public sealed record MessageType(Type Type)
{
    public string Name => Type.FullName!;
}
public sealed record ConsumerType(Type Type)
{
    public string Name => Type.FullName!;
}

public sealed record QueueDefinition(string Name, IReadOnlyDictionary<MessageType, List<ConsumerType>> Messages, IReadOnlyDictionary<ConsumerType, List<MessageType>> Consumers)
{
    public ushort ConsumerCount { get; init; } = 1;
    public ushort PrefetchCount { get; init; } = 1;
}
