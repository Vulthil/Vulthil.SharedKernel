using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging;

public interface IQueueConfigurator
{
    IQueueConfigurator AddConsumer<TConsumer>() where TConsumer : class, IConsumer;
    IQueueConfigurator AddRequestConsumer<TRequestConsumer>() where TRequestConsumer : class, IRequestConsumer;
}
