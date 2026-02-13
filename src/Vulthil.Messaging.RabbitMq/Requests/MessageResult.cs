using System.Diagnostics.CodeAnalysis;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed record MessageResult
{
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsSuccess { get; private set; }
    public byte[]? Value { get; private set; }
    public string? ErrorMessage { get; private set; }

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

    public static MessageResult Success(byte[] value) => new(true, value, null);
    public static MessageResult Failure(string errorMessage) => new(false, null, errorMessage);
}
