using Vulthil.Results;

namespace WebApi.Domain.MainEntities;

public static class MainEntityErrors
{
    public static readonly Error NameRequired =
        Error.Validation("MainEntity.NameRequired", "A main entity needs a name.");

    public static readonly Error NameTooLong =
        Error.Validation("MainEntity.NameTooLong", $"A name can have at most {MainEntity.MaxNameLength} characters.");

    public static readonly Error NameUnchanged =
        Error.Conflict("MainEntity.NameUnchanged", "The entity already has that name.");

    public static Error NotFound(Guid id) => Error.NotFound("MainEntity.NotFound", $"Entity with Id {id} was not found.");

    public static Error ReservedName(string name) =>
        Error.Conflict("MainEntity.ReservedName", $"The name '{name}' is reserved.");
}
