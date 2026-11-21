using FluentValidation;
using Shipments.Domain;
using Vulthil.SharedKernel.Application;

namespace Shipments.Application.Shipments;

/// <summary>
/// <c>WithError</c> gives the rule the code and message of a domain <see cref="Vulthil.Results.Error"/>, so the 400
/// response lists <c>Shipment.EstimateInThePast</c> under <c>errorCodes</c>. Validators come from DI, so this one can
/// take the <see cref="TimeProvider"/>.
/// </summary>
public sealed class RescheduleDeliveryCommandValidator : AbstractValidator<RescheduleDeliveryCommand>
{
    public RescheduleDeliveryCommandValidator(TimeProvider timeProvider) =>
        RuleFor(command => command.EstimatedDeliveryUtc)
            .Must(estimate => estimate > timeProvider.GetUtcNow())
            .WithError(ShipmentErrors.EstimateInThePast);
}
