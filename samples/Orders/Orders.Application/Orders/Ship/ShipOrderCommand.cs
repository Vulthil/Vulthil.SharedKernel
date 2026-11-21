using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Domain.MainEntities;

namespace WebApi.Application.Shipping;

public sealed record ShipMainEntityCommand(Guid Id, string Recipient) : ITransactionalCommand;

/// <summary>
/// Sends <see cref="CreateShipment"/> to the Shipments service's queue. The command is transactional, so the send is
/// captured by the outbox in the command's transaction and reaches the broker only after the transaction commits.
/// </summary>
public sealed class ShipMainEntityCommandHandler(IWebApiDbContext dbContext, ISendEndpointProvider sendEndpoints)
    : ICommandHandler<ShipMainEntityCommand>
{
    private static readonly Uri ShipmentCommands = new($"queue:{CreateShipment.QueueName}");

    public Task<Result> HandleAsync(ShipMainEntityCommand command, CancellationToken cancellationToken = default)
    {
        var id = new MainEntityId(command.Id);
        return dbContext.MainEntities
            .FirstOrDefaultAsync(mainEntity => mainEntity.Id == id, cancellationToken)
            .ToResultAsync(MainEntityErrors.NotFound(command.Id))
            .BindAsync(async mainEntity =>
            {
                var endpoint = await sendEndpoints.GetSendEndpointAsync(ShipmentCommands, cancellationToken);
                await endpoint.SendAsync(new CreateShipment(mainEntity.Id.Value, command.Recipient), cancellationToken);
                return Result.Success();
            });
    }
}
