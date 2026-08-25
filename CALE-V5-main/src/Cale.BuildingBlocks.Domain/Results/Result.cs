namespace Cale.BuildingBlocks.Domain.Results;

public readonly record struct Result
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static Result Ok() => new(true, null, null);

    public static Result Fail(string errorCode, string message) =>
        new(false, errorCode, message);
}

public readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(
        bool isSuccess,
        T? value,
        string? errorCode,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Ok(T value) => new(true, value, null, null);

    public static Result<T> Fail(string errorCode, string message) =>
        new(false, default, errorCode, message);
}
