using Vulthil.SharedKernel.Application.Messaging;

namespace Shipments.Application.Shipments;

/// <summary>
/// A command with no value to return: <see cref="ICommand"/> is an <c>ICommand&lt;Result&gt;</c>, and its handler
/// implements <c>ICommandHandler&lt;RescheduleDeliveryCommand&gt;</c>.
/// </summary>
public sealed record RescheduleDeliveryCommand(Guid ShipmentId, DateTimeOffset EstimatedDeliveryUtc) : ICommand;
