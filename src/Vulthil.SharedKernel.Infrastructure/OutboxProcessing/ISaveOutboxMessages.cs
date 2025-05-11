using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

public interface ISaveOutboxMessages : IUnitOfWork
{
    DbSet<OutboxMessage> OutboxMessages { get; }
};
