using Orders.Domain.Orders;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Application.Orders.PlaceMany;

public sealed record PlaceOrdersCommand(IReadOnlyList<string> Descriptions) : ITransactionalCommand<Result<IReadOnlyList<Guid>>>;

/// <summary>
/// Places every order or none. <c>Combine</c> folds the result of each <see cref="Order.Place"/> into one result
/// with all the orders, or the failure of the descriptions that were invalid.
/// </summary>
public sealed class PlaceOrdersCommandHandler(IOrdersDbContext dbContext)
    : ICommandHandler<PlaceOrdersCommand, Result<IReadOnlyList<Guid>>>
{
    public Task<Result<IReadOnlyList<Guid>>> HandleAsync(PlaceOrdersCommand command, CancellationToken cancellationToken = default) =>
        command.Descriptions
            .Select(Order.Place)
            .Combine()
            .Tap(dbContext.Orders.AddRange)
            .TapAsync(_ => dbContext.SaveChangesAsync(cancellationToken))
            .MapAsync(orders => (IReadOnlyList<Guid>)[.. orders.Select(order => order.Id.Value)]);
}
