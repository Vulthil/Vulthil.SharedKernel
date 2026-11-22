using Microsoft.EntityFrameworkCore;
using Orders.Domain.Fulfillments;
using Orders.Domain.Orders;
using Vulthil.SharedKernel.Application.Data;

namespace Orders.Application;

public interface IOrdersDbContext : IUnitOfWork
{
    DbSet<Order> Orders { get; }

    DbSet<Fulfillment> Fulfillments { get; }
}
