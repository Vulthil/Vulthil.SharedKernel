using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Domain.MainEntities;

namespace WebApi.Application.MainEntities.Create;

public sealed record CreateMainEntityCommand(string Name) : ITransactionalCommand<Result<Guid>>;

public sealed class CreateMainEntityCommandHandler(ILogger<CreateMainEntityCommandHandler> logger, IWebApiDbContext dbContext) : ICommandHandler<CreateMainEntityCommand, Result<Guid>>
{
    private readonly ILogger<CreateMainEntityCommandHandler> _logger = logger;
    private readonly IWebApiDbContext _dbContext = dbContext;

    public async Task<Result<Guid>> HandleAsync(CreateMainEntityCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating MainEntity With name: {Name}", command.Name);

        var mainEntity = MainEntity.Create(command.Name);
        _dbContext.MainEntities.Add(mainEntity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(mainEntity.Id.Value);
    }
}
