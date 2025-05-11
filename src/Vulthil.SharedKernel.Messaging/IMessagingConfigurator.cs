using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.SharedKernel.Messaging;
public sealed record RequestOption
{
    public required string RecipientQueueName { get; init; }
}

public sealed record EventOption(MessageType MessageType)
{
    public string ExchangeName { get; init; } = MessageType.Name;
    public string ExchangeType { get; init; } = ExchangeTypeMap.Fanout;
    public bool ExchangeAutoDelete { get; init; }
    public bool ExchangeDurable { get; init; } = true;
}

public static class ExchangeTypeMap
{
    public const string Fanout = "fanout";
    public const string Direct = "direct";
}

public interface IMessagingConfigurator
{
    IServiceCollection Services { get; }

    IMessagingConfigurator AddQueue(string queueName, Action<IQueueConfigurator> queueConfigurationAction);
    IMessagingConfigurator AddEvent<TEvent>(Action<EventOption>? eventOptionAction = null) where TEvent : class;
    IMessagingConfigurator AddRequest<TRequest>(string queueName, Action<RequestOption>? requestOptionAction = null)
        where TRequest : class;
}
