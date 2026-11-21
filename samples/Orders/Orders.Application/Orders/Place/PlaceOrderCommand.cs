using Orders.Domain.Orders;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Application.Orders.Place;

public sealed record PlaceOrderCommand(string Description) : ITransactionalCommand<Result<Guid>>;

public sealed class PlaceOrderCommandHandler(IOrdersDbContext dbContext) : ICommandHandler<PlaceOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(PlaceOrderCommand command, CancellationToken cancellationToken = default) =>
        await Order.Place(command.Description)
            .Tap(order => dbContext.Orders.Add(order))
            .TapAsync(_ => dbContext.SaveChangesAsync(cancellationToken))
            .MapAsync(order => order.Id.Value);
}
