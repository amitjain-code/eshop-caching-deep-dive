namespace EShop.SharedKernel;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    TooManyRequests,
}

/// <summary>An expected, domain-level failure. Exceptions are reserved for the unexpected.</summary>
public sealed record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error TooManyRequests(string code, string description) => new(code, description, ErrorType.TooManyRequests);
}
