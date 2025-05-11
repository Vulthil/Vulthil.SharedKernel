using Vulthil.SharedKernel.Messaging.Abstractions.Consumers;

namespace Vulthil.SharedKernel.Messaging;

public interface IQueueConfigurator
{
    IQueueConfigurator AddConsumer<TConsumer>() where TConsumer : class, IConsumer;
    IQueueConfigurator AddRequestConsumer<TRequestConsumer>() where TRequestConsumer : class, IRequestConsumer;
}
