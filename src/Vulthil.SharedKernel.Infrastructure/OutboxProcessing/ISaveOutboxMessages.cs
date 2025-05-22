using Microsoft.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

public interface ISaveOutboxMessages
{
    DbSet<OutboxMessage> OutboxMessages { get; }
};
