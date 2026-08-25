namespace Cale.BuildingBlocks.Domain.Exceptions;

public class DomainException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    public DomainException(
        string message,
        int statusCode = 400,
        string errorCode = "domain_error")
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}
