namespace Vulthil.SharedKernel.Abstractions;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}