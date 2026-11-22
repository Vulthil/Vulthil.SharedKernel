using Catalog.Search;
using Vulthil.Messaging;
using Vulthil.Messaging.Inbox.Cosmos;
using Vulthil.Messaging.RabbitMq;
using Vulthil.SharedKernel.Infrastructure;
using Vulthil.SharedKernel.Infrastructure.Cosmos;

namespace Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddCatalogPersistence(this IHostApplicationBuilder builder, string connectionStringKey)
    {
        builder.AddDbContext<CatalogDbContext>(database => database
            .UseCosmosDb(connectionStringKey, settings => settings.RequestTimeout = TimeSpan.FromSeconds(30))
            .EnableOutboxProcessing(outbox =>
            {
                // Each relayed message is dispatched in a scope of its own, so handlers run side by side without
                // sharing the relay's context.
                outbox.EnableParallelPublishing = true;
                outbox.MaxDegreeOfParallelism = 4;
                outbox.Retention.Enabled = true;
            }));

        builder.Services.AddCosmosInbox<CatalogDbContext>(inbox => inbox.Retention.Enabled = true);
        builder.Services.AddScoped<ISearchIndexStore>(sp => sp.GetRequiredService<CatalogDbContext>());

        return builder;
    }

    public static IHostApplicationBuilder AddCatalogMessaging(this IHostApplicationBuilder builder, string rabbitMqConnectionStringKey)
    {
        builder.AddMessaging(messaging =>
        {
            messaging.AddCatalogSearchMessaging();
            messaging.UseRabbitMq(rabbitMqConnectionStringKey);
        });

        return builder;
    }
}
