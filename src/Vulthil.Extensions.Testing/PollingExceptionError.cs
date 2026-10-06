using Vulthil.Results;

namespace Vulthil.Extensions.Testing;

/// <summary>
/// Represents a polling attempt in which the polled function threw an exception that
/// <see cref="PollingOptions.TreatAsFailedAttempt"/> accepted as a failed attempt.
/// </summary>
/// <remarks>
/// <see cref="PollingError.Errors"/> holds one of these for each such attempt, so a failing test can read the
/// exception and its stack trace.
/// </remarks>
/// <param name="Exception">The exception that the polled function threw.</param>
public sealed record PollingExceptionError(Exception Exception)
    : Error("Polling.Exception", (Exception ?? throw new ArgumentNullException(nameof(Exception))).Message, ErrorType.Failure);
