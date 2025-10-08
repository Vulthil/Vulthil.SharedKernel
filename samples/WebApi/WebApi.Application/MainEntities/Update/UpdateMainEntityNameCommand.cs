using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Domain.MainEntities;

namespace WebApi.Application.MainEntities.Update;

public sealed record UpdateMainEntityNameCommand(Guid Id, string Name) : ITransactionalCommand;

public sealed class UpdateMainEntityNameCommandHandler(ILogger<UpdateMainEntityNameCommandHandler> logger, IWebApiDbContext dbContext) : ICommandHandler<UpdateMainEntityNameCommand>
{
    private readonly ILogger<UpdateMainEntityNameCommandHandler> _logger = logger;
    private readonly IWebApiDbContext _dbContext = dbContext;

    public async Task<Result> HandleAsync(UpdateMainEntityNameCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating MainEntity With name: {Name}", command.Name);

        var id = new MainEntityId(command.Id);
        return await _dbContext.MainEntities.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            .ToResultAsync(MainEntityErrors.NotFound(command.Id))
            .TapAsync(e => e.UpdateName(command.Name))
            .TapAsync(() => _dbContext.SaveChangesAsync(cancellationToken));
    }
}
