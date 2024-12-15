namespace Vulthil.SharedKernel.Primitives;

public record Error
{
    public string Code { get; }
    public string Description { get; }
    public ErrorType Type { get; }
    protected internal Error(string code, string description, ErrorType type)
    {
        Code = code;
        Description = description;
        Type = type;
    }

    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    public static readonly Error NullValue = new("Error.NullValue", "Null value was provided", ErrorType.Failure);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);
    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);
    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);
    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);
}

public record ValidationError : Error
{
    public Error[] Errors { get; }

    public ValidationError(Error[] errors) : base("Validation.Errors", "One or more errors occured", ErrorType.Validation) => Errors = errors;
}

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
}
