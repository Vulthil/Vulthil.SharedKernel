using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Shipments.Application.Shipments;

public sealed record GetShipmentsByOrderQuery(Guid OrderReference) : IQuery<Result<IReadOnlyList<ShipmentDto>>>;
