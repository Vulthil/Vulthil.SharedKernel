using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shipments.Domain;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;
using Vulthil.Messaging.Inbox.Relational;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.MySql;

namespace Shipments.Infrastructure;

/// <summary>
/// The MySQL context of the Shipments service. <see cref="BaseDbContext"/> makes it the unit of work and the outbox
/// store; <see cref="ISaveInboxMessages"/> lets the relational inbox write its markers in the consumer's transaction.
/// </summary>
public sealed class ShipmentsDbContext(DbContextOptions<ShipmentsDbContext> options) : BaseDbContext(options), ISaveInboxMessages
{
    public DbSet<Shipment> Shipments => Set<Shipment>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override Assembly? ConfigurationAssembly => typeof(ShipmentsDbContext).Assembly;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMySqlOutbox();
        modelBuilder.ApplyRelationalInbox();
    }
}
