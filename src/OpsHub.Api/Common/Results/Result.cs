using System.Diagnostics.CodeAnalysis;

namespace OpsHub.Api.Common.Results;

/// <summary>
/// The outcome of an operation that can fail in an expected way: either a value or an
/// <see cref="Error"/>. Services return this instead of throwing, so failures are
/// visible in method signatures and controllers can map them to status codes.
/// </summary>
public sealed class Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

#pragma warning disable CA1000 // Static factory members on a generic type are the clearest API here.
    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) => new(default, error);
#pragma warning restore CA1000

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
