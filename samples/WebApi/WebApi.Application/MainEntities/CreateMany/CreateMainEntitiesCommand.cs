using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Domain.MainEntities;

namespace WebApi.Application.MainEntities.CreateMany;

public sealed record CreateMainEntitiesCommand(IReadOnlyList<string> Names) : ITransactionalCommand<Result<IReadOnlyList<Guid>>>;

/// <summary>
/// Creates every entity or none. <c>Combine</c> folds the result of each <see cref="MainEntity.Create"/> into one
/// result with all the entities, or the failure of the names that were invalid.
/// </summary>
public sealed class CreateMainEntitiesCommandHandler(IWebApiDbContext dbContext)
    : ICommandHandler<CreateMainEntitiesCommand, Result<IReadOnlyList<Guid>>>
{
    public Task<Result<IReadOnlyList<Guid>>> HandleAsync(CreateMainEntitiesCommand command, CancellationToken cancellationToken = default) =>
        command.Names
            .Select(MainEntity.Create)
            .Combine()
            .Tap(dbContext.MainEntities.AddRange)
            .TapAsync(_ => dbContext.SaveChangesAsync(cancellationToken))
            .MapAsync(mainEntities => (IReadOnlyList<Guid>)[.. mainEntities.Select(mainEntity => mainEntity.Id.Value)]);
}
