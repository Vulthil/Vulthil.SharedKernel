using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace WebApi.Application.WebApiEntity;

public sealed record CreateWebApiEntityCommand(string Name) : ITransactionalCommand<Result<Guid>>;

public sealed class CreateWebApiEntityCommandHandler(ILogger<CreateWebApiEntityCommandHandler> logger, IWebApiDbContext dbContext) : ICommandHandler<CreateWebApiEntityCommand, Result<Guid>>
{
    private readonly ILogger<CreateWebApiEntityCommandHandler> _logger = logger;
    private readonly IWebApiDbContext _dbContext = dbContext;

    public async Task<Result<Guid>> HandleAsync(CreateWebApiEntityCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating WebApiEntity With name: {Name}", command.Name);

        var webApiEntity = Domain.WebApiEntityModel.WebApiEntity.Create(command.Name);
        _dbContext.WebApiEntities.Add(webApiEntity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(webApiEntity.Id.Value);
    }
}
