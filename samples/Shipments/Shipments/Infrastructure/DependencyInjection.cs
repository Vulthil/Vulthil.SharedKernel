using Shipments.Application.Carrier;
using Shipments.Application.Shipments;
using Shipments.Application.Tracking;
using Shipments.Domain;
using Shipments.Messages;
using Vulthil.Messaging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Inbox;
using Vulthil.Messaging.Inbox.Relational;
using Vulthil.Messaging.Outbox;
using Vulthil.Messaging.RabbitMq;
using Vulthil.SharedKernel.Infrastructure;
using Vulthil.SharedKernel.Infrastructure.MySql;

namespace Shipments.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddShipmentsPersistence(this IHostApplicationBuilder builder, string connectionStringKey)
    {
        builder.AddDbContext<ShipmentsDbContext>(database => database
            .UseMySql(connectionStringKey, mySql => mySql.CommandTimeout(30))
            .EnableOutboxProcessing(outbox =>
            {
                outbox.BatchSize = 25;
                outbox.MaxRetries = 5;
                outbox.MaxPollingInterval = TimeSpan.FromSeconds(30);
                outbox.Retention.Enabled = true;
                outbox.Retention.RetentionPeriod = TimeSpan.FromDays(3);
                outbox.Retention.DeadLetterRetentionPeriod = TimeSpan.FromDays(30);
            }));

        builder.Services.AddRelationalInbox<ShipmentsDbContext>(inbox =>
        {
            inbox.Retention.Enabled = true;
            inbox.Retention.RetentionPeriod = TimeSpan.FromDays(14);
        });

        return builder;
    }

    public static IHostApplicationBuilder AddShipmentsMessaging(this IHostApplicationBuilder builder, string rabbitMqConnectionStringKey)
    {
        // Registered before AddIdempotentInbox, which then uses it as the TrackingUpdate key instead of the message id.
        builder.Services.AddSingleton<IInboxKeySelector<TrackingUpdate>, TrackingUpdateKeySelector>();

        builder.AddMessaging(messaging =>
        {
            ConfigureContracts(messaging);
            ConfigureTracking(messaging);

            messaging.ConfigureQueue(QueueNames.ShipmentCommands, queue =>
            {
                queue.UseRetry(retry =>
                {
                    retry.Exponential(4, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5));
                    retry.UseJitter(0.2);
                    // In-memory retry holds the delivery in the process, so jittered delays never queue up behind each
                    // other in the broker's shared retry queue.
                    retry.InMemory();
                });
                queue.UseDeadLetterQueue();
                queue.AddConsumer<CreateShipmentConsumer>();
            });

            messaging.ConfigureQueue(QueueNames.CarrierRequests, queue => queue.AddRequestConsumer<DeliveryEstimateRequestConsumer>());

            messaging.AddIdempotentInbox<CreateShipment>();
            messaging.AddIdempotentInbox<TrackingUpdate>();
            messaging.AddTransactionalOutbox();

            messaging.UseRabbitMq(rabbitMqConnectionStringKey);
        });

        return builder;
    }

    private static void ConfigureContracts(IMessagingConfigurator messaging)
    {
        messaging.ConfigureMessage<CreateShipment>(message => message.Urn = new Uri(CreateShipment.Urn));

        // The exchange name and type of ShipmentDelivered come from Messaging:Messages in appsettings.json; code adds
        // what configuration cannot express.
        messaging.ConfigureMessage<ShipmentDelivered>(message =>
        {
            message.Urn = new Uri(ShipmentDelivered.Urn);
            message.UseRoutingKey(delivered => $"shipment.delivered.{delivered.Carrier}");
            message.UseCorrelationId(delivered => delivered.OrderReference.ToString());
        });
    }

    private static void ConfigureTracking(IMessagingConfigurator messaging)
    {
        // Configuring the base type sets the defaults of every variant; each variant keeps its own exchange and URN.
        messaging.ConfigureMessage<TrackingUpdate>(message =>
        {
            message.ExchangeType = MessagingExchangeType.Topic;
            message.UseRoutingKey(update => $"tracking.{update.ShipmentId:N}");
        });

        // One partitioner for every variant: the scans of one shipment share a lane whatever their type.
        var shipmentLanes = new Partitioner(16);
        messaging.UsePartitioner<ParcelPickedUp>(shipmentLanes, ByShipment);
        messaging.UsePartitioner<ParcelInTransit>(shipmentLanes, ByShipment);
        messaging.UsePartitioner<ParcelDelivered>(shipmentLanes, ByShipment);

        messaging.ConfigureQueue(QueueNames.TrackingUpdates, queue =>
        {
            queue.UseSingleActiveConsumer();
            queue.SubscribeAll<TrackingUpdate>(typeof(TrackingUpdate).Assembly, "tracking.#");
            queue.UseRetry(retry =>
            {
                retry.Immediate(3);
                retry.Ignore<ShipmentNotFoundException>();
            });
            queue.UseDeadLetterQueue();
            queue.AddConsumer<TrackingUpdateConsumer>();
        });
    }

    private static string? ByShipment<TUpdate>(IMessageContext<TUpdate> context)
        where TUpdate : TrackingUpdate => context.Message.ShipmentId.ToString("N");
}
