using Microsoft.Extensions.Options;
using Shipments.Application.Carrier;
using Shipments.Application.Notifications;
using Shipments.Application.Retention;
using Vulthil.Extensions.Retention;

namespace Shipments.Application;

public static class ShipmentsModule
{
    /// <summary>
    /// Registers the services of the Shipments module: the customer notifier, the simulated carrier and the
    /// delivered-shipment retention sweep.
    /// </summary>
    public static IHostApplicationBuilder AddShipmentsModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ICustomerNotifier, LoggingCustomerNotifier>();

        builder.Services.AddOptions<CarrierOptions>().BindConfiguration(CarrierOptions.SectionName);
        builder.Services.AddHostedService<CarrierSimulator>();

        builder.AddDeliveredShipmentRetention();

        return builder;
    }

    private static void AddDeliveredShipmentRetention(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<ShipmentRetentionOptions>().BindConfiguration(ShipmentRetentionOptions.SectionName);
        builder.Services.AddScoped<DeliveredShipmentArchive>();

        var retention = builder.Configuration.GetSection(ShipmentRetentionOptions.SectionName).Get<ShipmentRetentionOptions>();
        if (retention is not { Enabled: true })
        {
            return;
        }

        builder.Services.AddRetentionSweep<DeliveredShipmentArchive>(
            "Delivered shipments",
            provider =>
            {
                var options = provider.GetRequiredService<IOptions<ShipmentRetentionOptions>>().Value;
                return new RetentionSweepSettings(options.RetentionPeriod, options.SweepInterval, options.BatchSize);
            },
            archive => new RetentionSweepDeleter(archive.DeleteDeliveredAsync));
    }
}
