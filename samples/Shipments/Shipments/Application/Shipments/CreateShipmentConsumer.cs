using Microsoft.Extensions.Options;
using Shipments.Application.Carrier;
using Shipments.Domain;
using Shipments.Infrastructure;
using Shipments.Messages;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Results;

namespace Shipments.Application.Shipments;

/// <summary>
/// Handles the <see cref="CreateShipment"/> command the WebApi sample sends to this service's queue. The inbox runs
/// it in a transaction and records the message id, so a redelivered command never creates a second shipment.
/// </summary>
public sealed class CreateShipmentConsumer(
    ShipmentsDbContext dbContext,
    IRequester requester,
    IOptions<CarrierOptions> carrierOptions,
    TimeProvider timeProvider,
    ILogger<CreateShipmentConsumer> logger) : IConsumer<CreateShipment>
{
    public async Task ConsumeAsync(IMessageContext<CreateShipment> messageContext, CancellationToken cancellationToken = default)
    {
        var command = messageContext.Message;
        logger.LogInformation(
            "Creating a shipment for order {OrderReference}, sent by {SourceService}",
            command.OrderReference,
            messageContext.Headers.GetValueOrDefault("source-service") ?? "an unknown service");

        var estimatedDelivery = await GetEstimatedDeliveryAsync(command.Recipient, cancellationToken);

        await Shipment.Create(command.OrderReference, command.Recipient, carrierOptions.Value.Name, timeProvider.GetUtcNow(), estimatedDelivery)
            .TapError(error => logger.LogWarning("Rejected shipment for order {OrderReference}: {Error}", command.OrderReference, error.Description))
            .Tap(shipment => dbContext.Shipments.Add(shipment))
            .TapAsync(_ => dbContext.SaveChangesAsync(cancellationToken));
    }

    /// <summary>
    /// A shipment does not wait for the carrier: when the estimate does not arrive in time, the shipment is created
    /// without one.
    /// </summary>
    private async Task<DateTimeOffset?> GetEstimatedDeliveryAsync(string recipient, CancellationToken cancellationToken)
    {
        var estimate = await requester.RequestAsync<GetDeliveryEstimate, DeliveryEstimate>(
            new GetDeliveryEstimate(recipient),
            requestContext =>
            {
                requestContext.SetTimeout(carrierOptions.Value.EstimateTimeout);
                return ValueTask.CompletedTask;
            },
            cancellationToken);

        return estimate.Match<DeliveryEstimate, DateTimeOffset?>(
            onSuccess: reply => reply.EstimatedDeliveryUtc,
            onFailure: error =>
            {
                if (error.Code == RequestErrorCodes.Timeout)
                {
                    logger.LogWarning("The carrier did not answer the delivery estimate in time");
                }
                else
                {
                    logger.LogWarning("The carrier could not give a delivery estimate: {Error}", error.Description);
                }

                return null;
            });
    }
}
