namespace OpsHub.Api.Common.Results;

/// <summary>The kinds of expected failure a service can report. Each maps to one HTTP status.</summary>
public enum ErrorType
{
    /// <summary>400: the input breaks a rule (field-level details in <see cref="Error.Fields"/>).</summary>
    Validation,

    /// <summary>404: the resource does not exist or the caller may not know it exists.</summary>
    NotFound,

    /// <summary>409: the request conflicts with the current state (duplicate, business rule).</summary>
    Conflict,

    /// <summary>403: the caller is signed in but not allowed to do this.</summary>
    Forbidden,
}

/// <summary>
/// An expected failure, such as "email already in use". Unexpected failures
/// (bugs, database down) are exceptions instead, handled by the global exception handler.
/// </summary>
/// <param name="Code">Stable, machine-readable identifier the frontend can rely on.</param>
/// <param name="Message">Human-readable explanation, safe to show to the user.</param>
public sealed record Error(
    ErrorType Type,
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Fields = null)
{
    public static Error NotFound(string code, string message) => new(ErrorType.NotFound, code, message);

    public static Error Conflict(string code, string message) => new(ErrorType.Conflict, code, message);

    public static Error Forbidden(string code, string message) => new(ErrorType.Forbidden, code, message);

    public static Error Validation(string field, string message) =>
        new(ErrorType.Validation, "validation_failed", "One or more fields are invalid.",
            new Dictionary<string, string[]> { [field] = [message] });
}
