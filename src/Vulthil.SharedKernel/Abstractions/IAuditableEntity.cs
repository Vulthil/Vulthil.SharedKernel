namespace Vulthil.SharedKernel.Abstractions;

public interface IAuditableEntity
{
    DateTimeOffset CreatedOnUtc { get; }

    DateTimeOffset? ModifiedOnUtc { get; }
}
