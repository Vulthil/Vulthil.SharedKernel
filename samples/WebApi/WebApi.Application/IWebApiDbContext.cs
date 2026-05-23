
using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Application.Data;
using WebApi.Domain.MainEntities;
using WebApi.Domain.SideEffects;

namespace WebApi.Application;

public interface IWebApiDbContext : IUnitOfWork
{
    DbSet<MainEntity> MainEntities { get; }
    DbSet<SideEffect> SideEffects { get; }
}
