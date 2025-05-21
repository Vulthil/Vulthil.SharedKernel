using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging;

internal sealed class QueueConfigurator(string queueName) : IQueueConfigurator
{
    private readonly Dictionary<ConsumerType, List<MessageType>> _consumers = [];
    private IReadOnlyDictionary<MessageType, List<ConsumerType>> _messages => _consumers
        .SelectMany(x => x.Value.Select(k => new { MessageType = k, ConsumerType = x.Key }))
        .GroupBy(x => x.MessageType, x => x.ConsumerType)
        .ToDictionary(x => x.Key, x => x.ToList());

    private readonly HashSet<MessageType> _requestMessages = [];

    public string QueueName { get; private set; } = queueName;
    public ushort ConsumerCount { get; set; } = 1;
    public ushort PrefetchCount { get; set; } = 1;

    public IQueueConfigurator AddConsumer<TConsumer>()
       where TConsumer : class, IConsumer
    {
        var consumerType = typeof(TConsumer);
        var types = Enumerable.Empty<MessageType>();
        if (consumerType.IsGenericType && consumerType.GetGenericTypeDefinition() == typeof(IConsumer<>))
        {
            types = types.Concat([new(consumerType.GetGenericArguments()[0])]);
        }

        var typeList = types.Concat(consumerType.GetInterfaces()
            .Where(x => x.IsGenericType)
            .Where(x => x.GetGenericTypeDefinition() == typeof(IConsumer<>))
            .Select(x => new MessageType(x.GetGenericArguments()[0])))
            .Distinct()
            .ToList();

        _consumers.Add(new(typeof(TConsumer)), typeList);

        return this;
    }
    public IQueueConfigurator AddRequestConsumer<TRequestConsumer>()
       where TRequestConsumer : class, IRequestConsumer
    {
        var consumerType = typeof(TRequestConsumer);
        var types = Enumerable.Empty<MessageType>();
        if (consumerType.IsGenericType && consumerType.GetGenericTypeDefinition() == typeof(IRequestConsumer<,>))
        {
            var messageType = new MessageType(consumerType.GetGenericArguments()[0]);

            types = types.Concat([messageType]);
        }

        var typeList = types.Concat(consumerType.GetInterfaces()
            .Where(x => x.IsGenericType)
            .Where(x => x.GetGenericTypeDefinition() == typeof(IRequestConsumer<,>))
            .Select(x => new MessageType(x.GetGenericArguments()[0])))
            .Distinct()
            .ToList();

        var existingRequestTypes = typeList
            .Where(_requestMessages.Contains);
        if (existingRequestTypes.Any())
        {
            throw new ArgumentException($"Request consumer for message types {existingRequestTypes} already registered.");
        }

        _requestMessages.UnionWith(typeList);
        _consumers.Add(new(typeof(TRequestConsumer)), typeList);

        return this;
    }

    internal QueueDefinition ToQueueDefinition() => new(QueueName, _messages, _consumers)
    {
        ConsumerCount = ConsumerCount,
        PrefetchCount = PrefetchCount,
    };
}
