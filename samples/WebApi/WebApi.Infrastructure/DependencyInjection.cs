using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging;
using Vulthil.Messaging.RabbitMq;
using Vulthil.SharedKernel.Infrastructure;
using WebApi.Application;
using WebApi.Infrastructure.Data;

namespace WebApi.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddDatabaseInfrastructure(this IHostApplicationBuilder builder, string connectionStringKey)
    {
        builder.Services
            .AddDbContext<IWebApiDbContext, WebApiDbContext>(databaseInfrastructureConfigurator =>
                    databaseInfrastructureConfigurator
                        .ConfigureDbContextOptions(options =>
                            options.UseNpgsql(builder.Configuration.GetConnectionString(connectionStringKey)))
                        .EnableOutboxProcessing());

        return builder;
    }

    public static IHostApplicationBuilder AddRabbitMqMessagingInfrastructure(this IHostApplicationBuilder builder, string rabbitMqConnectionStringKey)
    {
        builder.Services.AddMessaging(builder.Configuration, x =>
        {
            x.AddQueue("test", queue =>
            {
                queue.AddConsumer<TestConsumer>();
                queue.AddRequestConsumer<TestRequestConsumer>();
            });

            x.AddRequest<TestRequest>("test");

            x.UseRabbitMq();
        });

        builder.AddRabbitMqClient(rabbitMqConnectionStringKey);

        return builder;
    }
}
