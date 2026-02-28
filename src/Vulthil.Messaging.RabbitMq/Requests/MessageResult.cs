using System.Diagnostics.CodeAnalysis;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed record MessageResult
{
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsSuccess { get; private set; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public byte[]? Value { get; private set; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Executes this member.
    /// </summary>
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
    /// Executes this member.
    /// </summary>
    public static MessageResult Success(byte[] value) => new(true, value, null);
    /// <summary>
    /// Executes this member.
    /// </summary>
    public static MessageResult Failure(string errorMessage) => new(false, null, errorMessage);
}
