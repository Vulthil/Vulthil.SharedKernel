using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Domain.WebApiEntityModel;

namespace WebApi.Application.WebApiEntity;

public sealed record UpdateWebApiEntityCommand(Guid Id, string Name) : ITransactionalCommand;

public sealed class UpdateWebApiEntityCommandHandler(ILogger<UpdateWebApiEntityCommandHandler> logger, IWebApiDbContext dbContext) : ICommandHandler<UpdateWebApiEntityCommand>
{
    private readonly ILogger<UpdateWebApiEntityCommandHandler> _logger = logger;
    private readonly IWebApiDbContext _dbContext = dbContext;

    public async Task<Result> HandleAsync(UpdateWebApiEntityCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating WebApiEntity With name: {Name}", command.Name);

        var webEntityId = new WebApiEntityId(command.Id);
        var entity = await _dbContext.WebApiEntities.FirstOrDefaultAsync(w => w.Id == webEntityId);
        if (entity is null)
        {
            return Result.Failure(WebApiEntityErrors.NotFound(command.Id));
        }

        entity.UpdateName(command.Name);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
