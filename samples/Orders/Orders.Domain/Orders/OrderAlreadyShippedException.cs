using Vulthil.SharedKernel.Exceptions;

namespace WebApi.Domain.MainEntities;

/// <summary>
/// A <see cref="DomainException"/> carries an <see cref="Vulthil.Results.Error"/>, so the problem-details handler of
/// <c>Vulthil.SharedKernel.Api</c> answers it like a failed result with that error: here a 409 Conflict.
/// </summary>
public sealed class ReservedMainEntityNameException(string name) : DomainException(MainEntityErrors.ReservedName(name));
