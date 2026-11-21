using Vulthil.Results;
using Vulthil.SharedKernel.Primitives;
using WebApi.Domain.MainEntities.Events;

namespace WebApi.Domain.MainEntities;

public sealed record MainEntityId(Guid Value);

public class MainEntity : AggregateRoot<MainEntityId>
{
    public const int MaxNameLength = 100;

    private static readonly string[] ReservedNames = ["admin", "system"];

    public string Name { get; private set; }
    private MainEntity(string name) : base(new(Guid.CreateVersion7())) => Name = name;

    public static Result<MainEntity> Create(string name) =>
        ValidateName(name)
            .Map(validName => new MainEntity(validName))
            .Tap(mainEntity => mainEntity.Raise(new MainEntityCreatedEvent(mainEntity.Id)));

    /// <summary>
    /// Renames the entity. An invalid or unchanged name is an expected outcome and fails the result; a reserved name
    /// is a broken invariant and throws <see cref="ReservedMainEntityNameException"/>, which the API answers with the
    /// problem response of the exception's error.
    /// </summary>
    public Result Rename(string name)
    {
        if (ReservedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            throw new ReservedMainEntityNameException(name);
        }

        return ValidateName(name)
            .Ensure(validName => validName != Name, MainEntityErrors.NameUnchanged)
            .Bind(validName =>
            {
                Name = validName;
                Raise(new MainEntityNameUpdatedEvent(Id, Name));
                return Result.Success();
            });
    }

    private static Result<string> ValidateName(string name) =>
        Result.Success(name?.Trim() ?? string.Empty)
            .Ensure(trimmed => trimmed.Length > 0, MainEntityErrors.NameRequired)
            .Ensure(trimmed => trimmed.Length <= MaxNameLength, MainEntityErrors.NameTooLong);
}
