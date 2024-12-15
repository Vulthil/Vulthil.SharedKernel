namespace Vulthil.SharedKernel.Abstractions;

public interface ISoftDeletableEntity
{
    DateTimeOffset? DeletedOnUtc { get; }
}