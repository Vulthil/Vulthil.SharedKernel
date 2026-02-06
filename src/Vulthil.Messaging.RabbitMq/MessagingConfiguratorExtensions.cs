using Aspire.RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Messaging.RabbitMq.Requests;

namespace Vulthil.Messaging.RabbitMq;

public static class MessagingConfiguratorExtensions
{

    public static IMessagingConfigurator UseRabbitMq(this IMessagingConfigurator configurator,
        string connectionStringKey = "rabbitMq", // The name used in Aspire AppHost
        Action<RabbitMQClientSettings>? configureSettings = null,
        Action<ConnectionFactory>? configureConnectionFactory = null)
    {
        configurator.HostApplicationBuilder.AddRabbitMQClient(connectionStringKey, configureSettings, configureConnectionFactory);

        configurator.HostApplicationBuilder.Services.AddSingleton<RabbitMqBus>();
        configurator.HostApplicationBuilder.Services.AddSingleton<ITransport>(sp => sp.GetRequiredService<RabbitMqBus>());

        configurator.HostApplicationBuilder.Services.AddSingleton<RabbitMqPublisher>();
        configurator.HostApplicationBuilder.Services.AddSingleton<IPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());
        configurator.HostApplicationBuilder.Services.AddSingleton<IInternalPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());

        configurator.HostApplicationBuilder.Services.AddSingleton<ResponseListener>();
        configurator.HostApplicationBuilder.Services.AddScoped<IRequester, RabbitMqRequester>();

        return configurator;
    }
}
