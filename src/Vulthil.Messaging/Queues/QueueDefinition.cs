namespace Vulthil.Messaging.Queues;

public sealed record MessageType(Type Type)
{
    public string Name => Type.FullName!;
}

public sealed record ConsumerType(Type Type)
{
    public string Name => Type.FullName!;
}

public sealed record ConsumerDefinition(ConsumerType ConsumerType, IReadOnlyList<MessageType> MessageTypes)
{
    public string RoutingKey { get; set; } = string.Empty;
}
public abstract record Registration
{
    public required ConsumerType ConsumerType { get; init; }
    public required MessageType MessageType { get; init; }
    public string RoutingKey { get; set; } = "#";
}

public sealed record ConsumerRegistration : Registration;

public sealed record RequestConsumerRegistration : Registration
{
    public required Type ResponseType { get; init; }
}

public sealed record RequestHandlerDefinition(ConsumerType RequestConsumerType, IReadOnlyDictionary<MessageType, Type> RequestResponseTypes)
{
    public string RoutingKey { get; set; } = string.Empty;
}

public sealed record QueueDefinition(string Name)
{
    private readonly HashSet<Registration> _registrations = [];

    public string Name { get; set; } = Name;
    public ushort ConsumerCount { get; set; } = 1;
    public ushort PrefetchCount { get; set; } = 1;

    public bool IsQuorum { get; set; } = true;
    public bool Durable { get; set; } = true;
    public bool AutoDelete { get; set; }
    public bool Exclusive { get; set; }

    public MessagingExchangeType ExchangeType { get; set; } = MessagingExchangeType.Fanout;
    public bool ExchangeDurable { get; set; } = true;
    public bool ExchangeAutoDelete { get; set; }

    public Dictionary<string, object?> ExchangeArguments { get; } = [];

    public IEnumerable<Registration> Registrations => _registrations.AsReadOnly();

    internal void AddConsumer(Registration registration)
        => _registrations.Add(registration);
}
