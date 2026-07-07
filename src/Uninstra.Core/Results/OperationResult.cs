namespace Uninstra.Core.Results;

public sealed record ErrorDetails(string Code, string Message, string? TechnicalDetails = null);

public class OperationResult
{
    public bool IsSuccess { get; }
    public ErrorDetails? Error { get; }
    public List<string> Warnings { get; init; } = [];

    protected OperationResult(bool isSuccess, ErrorDetails? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static OperationResult Success() => new(true, null);
    public static OperationResult Failure(string code, string message, string? details = null)
        => new(false, new ErrorDetails(code, message, details));
    public static OperationResult Failure(ErrorDetails error) => new(false, error);

    public static OperationResult<T> Success<T>(T value) => new(value, null);
    public static OperationResult<T> Failure<T>(string code, string message, string? details = null)
        => new(default, new ErrorDetails(code, message, details));
}

public sealed class OperationResult<T> : OperationResult
{
    public T? Value { get; }

    internal OperationResult(T? value, ErrorDetails? error)
        : base(error is null, error)
    {
        Value = value;
    }

    public static OperationResult<T> Success(T value) => new(value, null);
    public new static OperationResult<T> Failure(string code, string message, string? details = null)
        => new(default, new ErrorDetails(code, message, details));
}
