using System.Diagnostics.CodeAnalysis;

namespace Vulthil.Messaging.RabbitMq.Requests;

/// <summary>
/// Wire-level envelope for an RPC reply: either a success carrying the serialized response payload,
/// or a failure carrying an error message. Written to the reply queue by the request handler and
/// read back by <see cref="ResponseWaiter{T}"/>.
/// </summary>
internal sealed record MessageResult
{
    /// <summary>
    /// Gets a value indicating whether the request was handled successfully. When <see langword="true"/>,
    /// <see cref="Value"/> is non-null; otherwise <see cref="ErrorMessage"/> is non-null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsSuccess { get; private set; }
    /// <summary>
    /// Gets the serialized response payload, or <see langword="null"/> when the request failed.
    /// </summary>
    public byte[]? Value { get; private set; }
    /// <summary>
    /// Gets the error description, or <see langword="null"/> when the request succeeded.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Initializes a new <see cref="MessageResult"/>, validating that a success carries a
    /// <paramref name="value"/> and a failure carries an <paramref name="errorMessage"/>.
    /// </summary>
    /// <param name="isSuccess">Whether the request was handled successfully.</param>
    /// <param name="value">The serialized response payload; required when <paramref name="isSuccess"/> is <see langword="true"/>.</param>
    /// <param name="errorMessage">The error description; required when <paramref name="isSuccess"/> is <see langword="false"/>.</param>
    public MessageResult(bool isSuccess, byte[]? value, string? errorMessage = null)
    {
        if (isSuccess && value is null)
        {
            ArgumentNullException.ThrowIfNull(value);
        }
        else if (!isSuccess && string.IsNullOrWhiteSpace(errorMessage))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        }

        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a successful result wrapping the serialized response payload.
    /// </summary>
    /// <param name="value">The serialized response payload.</param>
    /// <returns>A successful <see cref="MessageResult"/>.</returns>
    public static MessageResult Success(byte[] value) => new(true, value, null);
    /// <summary>
    /// Creates a failed result carrying the specified error description.
    /// </summary>
    /// <param name="errorMessage">The error description.</param>
    /// <returns>A failed <see cref="MessageResult"/>.</returns>
    public static MessageResult Failure(string errorMessage) => new(false, null, errorMessage);
}
