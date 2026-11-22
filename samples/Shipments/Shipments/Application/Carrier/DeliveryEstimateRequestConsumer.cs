using Microsoft.Extensions.Options;
using Shipments.Messages;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Shipments.Application.Carrier;

/// <summary>
/// Plays the carrier's side of the request/reply call. The requester receives the returned value as a
/// <c>Result&lt;DeliveryEstimate&gt;</c>; an exception thrown here would reach it as a failed result instead.
/// </summary>
public sealed class DeliveryEstimateRequestConsumer(IOptions<CarrierOptions> options, TimeProvider timeProvider)
    : IRequestConsumer<GetDeliveryEstimate, DeliveryEstimate>
{
    public Task<DeliveryEstimate> ConsumeAsync(IMessageContext<GetDeliveryEstimate> messageContext, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DeliveryEstimate(timeProvider.GetUtcNow().Add(options.Value.TransitTime)));
}
