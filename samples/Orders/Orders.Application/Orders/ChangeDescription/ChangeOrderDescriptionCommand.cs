using Microsoft.EntityFrameworkCore;
using Orders.Domain.Orders;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Application.Orders.ChangeDescription;

public sealed record ChangeOrderDescriptionCommand(Guid OrderId, string Description) : ITransactionalCommand;

public sealed class ChangeOrderDescriptionCommandHandler(IOrdersDbContext dbContext) : ICommandHandler<ChangeOrderDescriptionCommand>
{
    public async Task<Result> HandleAsync(ChangeOrderDescriptionCommand command, CancellationToken cancellationToken = default)
    {
        var id = new OrderId(command.OrderId);
        return await dbContext.Orders.FirstOrDefaultAsync(order => order.Id == id, cancellationToken)
            .ToResultAsync(OrderErrors.NotFound(command.OrderId))
            .BindAsync(order => order.ChangeDescription(command.Description))
            .TapAsync(() => dbContext.SaveChangesAsync(cancellationToken));
    }
}
