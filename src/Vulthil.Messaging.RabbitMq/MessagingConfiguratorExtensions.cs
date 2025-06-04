using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging;
using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.RabbitMq;

public static class MessagingConfiguratorExtensions
{
    public static IMessagingConfigurator UseRabbitMq(this IMessagingConfigurator configurator)
    {
        configurator.Services.AddSingleton<ITransport, RabbitMqTransportService>();

        configurator.Services.AddSingleton<RabbitMqRequester>();
        configurator.Services.AddSingleton<IRequester>(sp => sp.GetRequiredService<RabbitMqRequester>());
        configurator.Services.AddSingleton<IPublisher, RabbitMqPublisher>();

        return configurator;
    }

    public static IHostApplicationBuilder AddRabbitMqClient(this IHostApplicationBuilder builder, string connectionStringKey)
    {
        builder.AddRabbitMQClient(connectionStringKey);

        return builder;
    }
}
