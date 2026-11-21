using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Shipments.Application.Shipments;

public sealed record GetShipmentQuery(Guid Id) : IQuery<Result<ShipmentDto>>;
